using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Npgsql;
using SlateIQ.Application.Abstractions.Audit;
using SlateIQ.Application.Abstractions.Persistence;
using SlateIQ.Domain.AuditOps;

namespace SlateIQ.Api.Endpoints;

public sealed record CreateFindingRequest(
    Guid AuditAssignmentId,
    Guid ShowId,
    string Severity,
    string FindingCategory,
    string Title,
    string Description,
    string Visibility = "SHARED_WITH_PRODUCTION",
    Guid? DepartmentId = null,
    string? LinkedEntityType = null,
    Guid? LinkedEntityId = null,
    DateOnly? DueDate = null);

public sealed record FindingDto(
    Guid FindingId,
    Guid ShowId,
    string Severity,
    string FindingCategory,
    string Title,
    string Description,
    string Visibility,
    string Status,
    DateOnly? DueDate);

public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/audit");

        group.MapGet("/findings", ListFindingsAsync);
        group.MapPost("/findings", CreateFindingAsync);
        group.MapPost("/findings/{id:guid}/open", OpenFindingAsync);
        group.MapPost("/findings/{id:guid}/resolve", ResolveFindingAsync);

        return endpoints;
    }

    private static async Task<IResult> ListFindingsAsync(
        NpgsqlDataSource dataSource,
        ITenantContext tenantContext,
        CancellationToken ct)
    {
        var orgId = tenantContext.CurrentOrgId;
        if (!orgId.HasValue) return Results.Unauthorized();

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        const string sql = @"
            SELECT finding_id, show_id, severity, finding_category, title, description,
                   visibility, status, due_date
            FROM auditops.audit_finding
            ORDER BY created_at DESC;";

        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var list = new List<FindingDto>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new FindingDto(
                FindingId: reader.GetGuid(0),
                ShowId: reader.GetGuid(1),
                Severity: reader.GetString(2),
                FindingCategory: reader.GetString(3),
                Title: reader.GetString(4),
                Description: reader.GetString(5),
                Visibility: reader.GetString(6),
                Status: reader.GetString(7),
                DueDate: reader.IsDBNull(8) ? null : (DateOnly?)DateOnly.FromDateTime(reader.GetDateTime(8))));
        }

        return Results.Ok(list);
    }

    private static async Task<IResult> CreateFindingAsync(
        CreateFindingRequest request,
        NpgsqlDataSource dataSource,
        ITenantContext tenantContext,
        IAuditWriter auditWriter,
        CancellationToken ct)
    {
        var orgId = tenantContext.CurrentOrgId;
        if (!orgId.HasValue) return Results.Unauthorized();

        if (!Enum.TryParse<FindingSeverity>(request.Severity, true, out var severity))
        {
            return Results.BadRequest(new { error = "ERR-080", message = "Invalid severity level." });
        }

        var userId = Guid.NewGuid();
        var finding = AuditFinding.Create(
            orgId: orgId.Value,
            auditAssignmentId: request.AuditAssignmentId,
            showId: request.ShowId,
            severity: severity,
            category: request.FindingCategory,
            title: request.Title,
            description: request.Description,
            createdBy: userId,
            departmentId: request.DepartmentId,
            linkedEntityType: request.LinkedEntityType,
            linkedEntityId: request.LinkedEntityId,
            dueDate: request.DueDate);

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        const string sql = @"
            INSERT INTO auditops.audit_finding (
                org_id, finding_id, audit_assignment_id, show_id, department_id, severity,
                finding_category, title, description, linked_entity_type, linked_entity_id,
                visibility, due_date, status, created_at, created_by, updated_at, updated_by, row_version
            ) VALUES (
                @orgId, @id, @assignmentId, @showId, @deptId, @sev,
                @cat, @title, @desc, @linkedType, @linkedId,
                @vis, @due, 'DRAFT', now(), @createdBy, now(), @createdBy, 1
            );";

        await using (var cmd = new NpgsqlCommand(sql, connection))
        {
            cmd.Parameters.AddWithValue("orgId", finding.OrgId);
            cmd.Parameters.AddWithValue("id", finding.FindingId);
            cmd.Parameters.AddWithValue("assignmentId", finding.AuditAssignmentId);
            cmd.Parameters.AddWithValue("showId", finding.ShowId);
            cmd.Parameters.AddWithValue("deptId", (object?)finding.DepartmentId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("sev", finding.Severity.ToString());
            cmd.Parameters.AddWithValue("cat", finding.FindingCategory);
            cmd.Parameters.AddWithValue("title", finding.Title);
            cmd.Parameters.AddWithValue("desc", finding.Description);
            cmd.Parameters.AddWithValue("linkedType", (object?)finding.LinkedEntityType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("linkedId", (object?)finding.LinkedEntityId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("vis", finding.Visibility.ToString());
            cmd.Parameters.AddWithValue("due", (object?)finding.DueDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("createdBy", userId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await auditWriter.WriteEventAsync(new AuditEventRecord(
            OrgId: orgId.Value,
            ActorUserId: userId,
            Action: "audit.finding_created",
            EntityType: "finding",
            EntityId: finding.FindingId,
            Metadata: JsonSerializer.Serialize(new { title = finding.Title, severity = finding.Severity.ToString() })), ct);

        return Results.Created($"/api/v1/audit/findings/{finding.FindingId}", new { findingId = finding.FindingId });
    }

    private static async Task<IResult> OpenFindingAsync(
        Guid id,
        NpgsqlDataSource dataSource,
        ITenantContext tenantContext,
        IAuditWriter auditWriter,
        CancellationToken ct)
    {
        var orgId = tenantContext.CurrentOrgId;
        if (!orgId.HasValue) return Results.Unauthorized();

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        const string sql = @"
            UPDATE auditops.audit_finding
            SET status = 'OPEN', updated_at = now(), row_version = row_version + 1
            WHERE finding_id = @id AND status = 'DRAFT';";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("id", id);
        int rows = await cmd.ExecuteNonQueryAsync(ct);

        if (rows == 0) return Results.BadRequest(new { error = "ERR-081", message = "Finding must be in DRAFT state to open." });

        return Results.Ok(new { message = "Finding opened and visible to production team." });
    }

    private static async Task<IResult> ResolveFindingAsync(
        Guid id,
        NpgsqlDataSource dataSource,
        ITenantContext tenantContext,
        IAuditWriter auditWriter,
        CancellationToken ct)
    {
        var orgId = tenantContext.CurrentOrgId;
        if (!orgId.HasValue) return Results.Unauthorized();

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        const string sql = @"
            UPDATE auditops.audit_finding
            SET status = 'RESOLVED', updated_at = now(), row_version = row_version + 1
            WHERE finding_id = @id AND status = 'OPEN';";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("id", id);
        int rows = await cmd.ExecuteNonQueryAsync(ct);

        if (rows == 0) return Results.BadRequest(new { error = "ERR-082", message = "Finding must be in OPEN state to resolve." });

        return Results.Ok(new { message = "Finding marked as resolved." });
    }
}
