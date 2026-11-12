using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Npgsql;
using SlateIQ.Application.Abstractions.Audit;
using SlateIQ.Application.Abstractions.Ops;
using SlateIQ.Application.Abstractions.Persistence;
using SlateIQ.Domain.Ops;
using SlateIQ.Domain.Production;

namespace SlateIQ.Api.Endpoints;

public sealed record CreateShowRequest(
    string Code,
    string Title,
    string ShowType,
    string? Format = null,
    string? Genre = null,
    string Language = "English",
    DateOnly? PlannedStartDate = null,
    DateOnly? PlannedEndDate = null,
    int? PlannedShootDays = null);

public sealed record ShowResponseDto(
    Guid ShowId,
    Guid OrgId,
    string Code,
    string Title,
    string ShowType,
    string Status,
    string? Format,
    string? Genre,
    string Language,
    DateOnly? PlannedStartDate,
    DateOnly? PlannedEndDate,
    int? PlannedShootDays,
    long RowVersion);

public static class ProductionEndpoints
{
    public static IEndpointRouteBuilder MapProductionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/shows");

        group.MapGet("/", ListShowsAsync);
        group.MapPost("/", CreateShowAsync);
        group.MapGet("/{id:guid}", GetShowByIdAsync);
        group.MapPost("/{id:guid}/activate", ActivateShowAsync);

        return endpoints;
    }

    private static async Task<IResult> ListShowsAsync(
        NpgsqlDataSource dataSource,
        ITenantContext tenantContext,
        CancellationToken ct)
    {
        var orgId = tenantContext.CurrentOrgId;
        if (!orgId.HasValue) return Results.Unauthorized();

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        const string sql = @"
            SELECT show_id, org_id, code, title, show_type, status, format, genre, language,
                   planned_start_date, planned_end_date, planned_shoot_days, row_version
            FROM prod.show
            ORDER BY created_at DESC;";

        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var list = new List<ShowResponseDto>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new ShowResponseDto(
                ShowId: reader.GetGuid(0),
                OrgId: reader.GetGuid(1),
                Code: reader.GetString(2),
                Title: reader.GetString(3),
                ShowType: reader.GetString(4),
                Status: reader.GetString(5),
                Format: reader.IsDBNull(6) ? null : reader.GetString(6),
                Genre: reader.IsDBNull(7) ? null : reader.GetString(7),
                Language: reader.GetString(8),
                PlannedStartDate: reader.IsDBNull(9) ? null : DateOnly.FromDateTime(reader.GetDateTime(9)),
                PlannedEndDate: reader.IsDBNull(10) ? null : DateOnly.FromDateTime(reader.GetDateTime(10)),
                PlannedShootDays: reader.IsDBNull(11) ? null : reader.GetInt32(11),
                RowVersion: reader.GetInt64(12)));
        }

        return Results.Ok(list);
    }

    private static async Task<IResult> CreateShowAsync(
        CreateShowRequest request,
        NpgsqlDataSource dataSource,
        ITenantContext tenantContext,
        IAuditWriter auditWriter,
        IOutboxPublisher outboxPublisher,
        CancellationToken ct)
    {
        var orgId = tenantContext.CurrentOrgId;
        if (!orgId.HasValue) return Results.Unauthorized();

        if (!Enum.TryParse<ShowType>(request.ShowType, true, out var showType))
        {
            return Results.BadRequest(new { error = "ERR-040", message = $"Invalid show type '{request.ShowType}'." });
        }

        var userId = Guid.NewGuid(); // Derived from auth context
        Show show;
        try
        {
            show = Show.Create(
                orgId: orgId.Value,
                code: request.Code,
                title: request.Title,
                showType: showType,
                createdBy: userId,
                format: request.Format,
                genre: request.Genre,
                language: request.Language,
                plannedStartDate: request.PlannedStartDate,
                plannedEndDate: request.PlannedEndDate,
                plannedShootDays: request.PlannedShootDays);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = "ERR-041", message = ex.Message });
        }

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        // 1. Insert Show record
        const string insertShowSql = @"
            INSERT INTO prod.show (
                org_id, show_id, code, title, show_type, format, genre, language,
                planned_start_date, planned_end_date, planned_shoot_days, hosting_pattern,
                status, created_at, created_by, updated_at, updated_by, row_version
            ) VALUES (
                @orgId, @showId, @code, @title, @showType, @format, @genre, @language,
                @startDate, @endDate, @shootDays, @hosting,
                @status, @createdAt, @createdBy, @updatedAt, @updatedBy, @version
            );";

        await using (var cmd = new NpgsqlCommand(insertShowSql, connection, tx))
        {
            cmd.Parameters.AddWithValue("orgId", show.OrgId);
            cmd.Parameters.AddWithValue("showId", show.ShowId);
            cmd.Parameters.AddWithValue("code", show.Code);
            cmd.Parameters.AddWithValue("title", show.Title);
            cmd.Parameters.AddWithValue("showType", show.ShowType.ToString());
            cmd.Parameters.AddWithValue("format", (object?)show.Format ?? DBNull.Value);
            cmd.Parameters.AddWithValue("genre", (object?)show.Genre ?? DBNull.Value);
            cmd.Parameters.AddWithValue("language", show.Language);
            cmd.Parameters.AddWithValue("startDate", (object?)show.PlannedStartDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("endDate", (object?)show.PlannedEndDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("shootDays", (object?)show.PlannedShootDays ?? DBNull.Value);
            cmd.Parameters.AddWithValue("hosting", show.HostingPattern.ToString());
            cmd.Parameters.AddWithValue("status", show.Status.ToString());
            cmd.Parameters.AddWithValue("createdAt", show.CreatedAt);
            cmd.Parameters.AddWithValue("createdBy", show.CreatedBy);
            cmd.Parameters.AddWithValue("updatedAt", show.UpdatedAt);
            cmd.Parameters.AddWithValue("updatedBy", show.UpdatedBy);
            cmd.Parameters.AddWithValue("version", show.RowVersion);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // 2. Automatically create Season 1
        var season = Season.Create(orgId.Value, show.ShowId, 1, userId, "Season 1");
        const string insertSeasonSql = @"
            INSERT INTO prod.season (
                org_id, season_id, show_id, season_number, title, status,
                created_at, created_by, updated_at, updated_by, row_version
            ) VALUES (
                @orgId, @seasonId, @showId, @seasonNo, @title, @status,
                @createdAt, @createdBy, @updatedAt, @updatedBy, @version
            );";

        await using (var cmd = new NpgsqlCommand(insertSeasonSql, connection, tx))
        {
            cmd.Parameters.AddWithValue("orgId", season.OrgId);
            cmd.Parameters.AddWithValue("seasonId", season.SeasonId);
            cmd.Parameters.AddWithValue("showId", season.ShowId);
            cmd.Parameters.AddWithValue("seasonNo", season.SeasonNumber);
            cmd.Parameters.AddWithValue("title", (object?)season.Title ?? DBNull.Value);
            cmd.Parameters.AddWithValue("status", season.Status.ToString());
            cmd.Parameters.AddWithValue("createdAt", season.CreatedAt);
            cmd.Parameters.AddWithValue("createdBy", season.CreatedBy);
            cmd.Parameters.AddWithValue("updatedAt", season.UpdatedAt);
            cmd.Parameters.AddWithValue("updatedBy", season.UpdatedBy);
            cmd.Parameters.AddWithValue("version", season.RowVersion);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);

        // 3. Write in-transaction hash-chained audit event
        await auditWriter.WriteEventAsync(new AuditEventRecord(
            OrgId: orgId.Value,
            ActorUserId: userId,
            Action: "show.created",
            EntityType: "show",
            EntityId: show.ShowId,
            Metadata: JsonSerializer.Serialize(new { code = show.Code, title = show.Title, showType = show.ShowType.ToString() })), ct);

        // 4. Publish ShowCreated outbox event
        await outboxPublisher.PublishAsync(OutboxEvent.Create(
            aggregateType: "Show",
            aggregateId: show.ShowId,
            eventType: "ShowCreated",
            payload: new { showId = show.ShowId, orgId = show.OrgId, code = show.Code, title = show.Title },
            orgId: orgId.Value), ct);

        var responseDto = new ShowResponseDto(
            show.ShowId, show.OrgId, show.Code, show.Title, show.ShowType.ToString(), show.Status.ToString(),
            show.Format, show.Genre, show.Language, show.PlannedStartDate, show.PlannedEndDate, show.PlannedShootDays, show.RowVersion);

        return Results.Created($"/api/v1/shows/{show.ShowId}", responseDto);
    }

    private static async Task<IResult> GetShowByIdAsync(
        Guid id,
        NpgsqlDataSource dataSource,
        ITenantContext tenantContext,
        CancellationToken ct)
    {
        var orgId = tenantContext.CurrentOrgId;
        if (!orgId.HasValue) return Results.Unauthorized();

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        const string sql = @"
            SELECT show_id, org_id, code, title, show_type, status, format, genre, language,
                   planned_start_date, planned_end_date, planned_shoot_days, row_version
            FROM prod.show
            WHERE show_id = @id;";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("id", id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return Results.NotFound(new { error = "ERR-042", message = "Show not found." });
        }

        var dto = new ShowResponseDto(
            ShowId: reader.GetGuid(0),
            OrgId: reader.GetGuid(1),
            Code: reader.GetString(2),
            Title: reader.GetString(3),
            ShowType: reader.GetString(4),
            Status: reader.GetString(5),
            Format: reader.IsDBNull(6) ? null : reader.GetString(6),
            Genre: reader.IsDBNull(7) ? null : reader.GetString(7),
            Language: reader.GetString(8),
            PlannedStartDate: reader.IsDBNull(9) ? null : DateOnly.FromDateTime(reader.GetDateTime(9)),
            PlannedEndDate: reader.IsDBNull(10) ? null : DateOnly.FromDateTime(reader.GetDateTime(10)),
            PlannedShootDays: reader.IsDBNull(11) ? null : reader.GetInt32(11),
            RowVersion: reader.GetInt64(12));

        return Results.Ok(dto);
    }

    private static async Task<IResult> ActivateShowAsync(
        Guid id,
        NpgsqlDataSource dataSource,
        ITenantContext tenantContext,
        IAuditWriter auditWriter,
        IOutboxPublisher outboxPublisher,
        CancellationToken ct)
    {
        var orgId = tenantContext.CurrentOrgId;
        if (!orgId.HasValue) return Results.Unauthorized();

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        const string selectSql = "SELECT status FROM prod.show WHERE show_id = @id FOR UPDATE;";
        string? currentStatus;

        await using (var cmd = new NpgsqlCommand(selectSql, connection))
        {
            cmd.Parameters.AddWithValue("id", id);
            currentStatus = (string?)await cmd.ExecuteScalarAsync(ct);
        }

        if (currentStatus == null)
        {
            return Results.NotFound(new { error = "ERR-042", message = "Show not found." });
        }

        if (currentStatus != "PLANNING")
        {
            return Results.BadRequest(new { error = "ERR-043", message = $"Cannot activate show in state '{currentStatus}'." });
        }

        const string updateSql = @"
            UPDATE prod.show 
            SET status = 'ACTIVE', updated_at = now(), row_version = row_version + 1
            WHERE show_id = @id;";

        await using (var cmd = new NpgsqlCommand(updateSql, connection))
        {
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        var userId = Guid.NewGuid();
        await auditWriter.WriteEventAsync(new AuditEventRecord(
            OrgId: orgId.Value,
            ActorUserId: userId,
            Action: "show.status_changed",
            EntityType: "show",
            EntityId: id,
            PreviousState: "{\"status\": \"PLANNING\"}",
            NewState: "{\"status\": \"ACTIVE\"}"), ct);

        await outboxPublisher.PublishAsync(OutboxEvent.Create(
            aggregateType: "Show",
            aggregateId: id,
            eventType: "ShowStatusChanged",
            payload: new { showId = id, oldStatus = "PLANNING", newStatus = "ACTIVE" },
            orgId: orgId.Value), ct);

        return Results.Ok(new { message = "Show successfully activated." });
    }
}
