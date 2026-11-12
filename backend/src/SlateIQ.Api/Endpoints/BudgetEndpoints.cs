using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Npgsql;
using SlateIQ.Application.Abstractions.Audit;
using SlateIQ.Application.Abstractions.Ops;
using SlateIQ.Application.Abstractions.Persistence;
using SlateIQ.Domain.Budgeting;
using SlateIQ.Domain.Common.ValueObjects;
using SlateIQ.Domain.Ops;

namespace SlateIQ.Api.Endpoints;

public sealed record CreateBudgetLineRequest(
    Guid? DepartmentId,
    Guid? CategoryId,
    string CostType,
    decimal Amount,
    string LineType = "OPERATIONAL",
    decimal? Quantity = null,
    string? Unit = null,
    decimal? Rate = null,
    string? Notes = null);

public sealed record BudgetDto(
    Guid BudgetId,
    Guid OrgId,
    Guid ShowId,
    Guid SeasonId,
    string Status,
    Guid? CurrentVersionId,
    string BaseCurrency,
    BudgetVersionDto? ActiveVersion);

public sealed record BudgetVersionDto(
    Guid BudgetVersionId,
    int VersionNo,
    string VersionType,
    string Status,
    decimal TotalAmount,
    decimal ContingencyAmount,
    IReadOnlyList<BudgetLineDto> Lines);

public sealed record BudgetLineDto(
    Guid BudgetLineId,
    Guid? DepartmentId,
    Guid? CategoryId,
    string CostType,
    string LineType,
    decimal Amount,
    string? Notes);

public static class BudgetEndpoints
{
    public static IEndpointRouteBuilder MapBudgetEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1");

        group.MapGet("/shows/{showId:guid}/seasons/{seasonId:guid}/budget", GetBudgetAsync);
        group.MapPost("/shows/{showId:guid}/seasons/{seasonId:guid}/budget", CreateBudgetContainerAsync);
        group.MapPost("/budgets/{budgetId:guid}/versions/{versionId:guid}/lines", AddBudgetLineAsync);
        group.MapPost("/budgets/{budgetId:guid}/versions/{versionId:guid}/submit", SubmitBudgetVersionAsync);
        group.MapPost("/budgets/{budgetId:guid}/versions/{versionId:guid}/approve", ApproveBudgetVersionAsync);

        return endpoints;
    }

    private static async Task<IResult> GetBudgetAsync(
        Guid showId,
        Guid seasonId,
        NpgsqlDataSource dataSource,
        ITenantContext tenantContext,
        CancellationToken ct)
    {
        var orgId = tenantContext.CurrentOrgId;
        if (!orgId.HasValue) return Results.Unauthorized();

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        const string sql = @"
            SELECT budget_id, org_id, show_id, season_id, status, current_version_id, base_currency
            FROM budget.budget
            WHERE show_id = @showId AND season_id = @seasonId;";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("showId", showId);
        cmd.Parameters.AddWithValue("seasonId", seasonId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return Results.NotFound(new { error = "ERR-050", message = "Budget not found for show/season." });
        }

        var budgetId = reader.GetGuid(0);
        var status = reader.GetString(4);
        var currentVersionId = reader.IsDBNull(5) ? null : (Guid?)reader.GetGuid(5);
        var baseCurrency = reader.GetString(6);

        var dto = new BudgetDto(budgetId, orgId.Value, showId, seasonId, status, currentVersionId, baseCurrency, null);
        return Results.Ok(dto);
    }

    private static async Task<IResult> CreateBudgetContainerAsync(
        Guid showId,
        Guid seasonId,
        NpgsqlDataSource dataSource,
        ITenantContext tenantContext,
        IAuditWriter auditWriter,
        CancellationToken ct)
    {
        var orgId = tenantContext.CurrentOrgId;
        if (!orgId.HasValue) return Results.Unauthorized();

        var userId = Guid.NewGuid();
        var budget = Budget.Create(orgId.Value, showId, seasonId, userId, "INR");
        var version = BudgetVersion.CreateOriginalDraft(orgId.Value, budget.BudgetId, showId, "INR", userId);

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        const string insertBudgetSql = @"
            INSERT INTO budget.budget (
                org_id, budget_id, show_id, season_id, status, base_currency,
                created_at, created_by, updated_at, updated_by, row_version
            ) VALUES (
                @orgId, @budgetId, @showId, @seasonId, @status, @ccy,
                @createdAt, @createdBy, @updatedAt, @updatedBy, 1
            );";

        await using (var cmd = new NpgsqlCommand(insertBudgetSql, connection, tx))
        {
            cmd.Parameters.AddWithValue("orgId", budget.OrgId);
            cmd.Parameters.AddWithValue("budgetId", budget.BudgetId);
            cmd.Parameters.AddWithValue("showId", budget.ShowId);
            cmd.Parameters.AddWithValue("seasonId", budget.SeasonId);
            cmd.Parameters.AddWithValue("status", budget.Status.ToString());
            cmd.Parameters.AddWithValue("ccy", budget.BaseCurrency);
            cmd.Parameters.AddWithValue("createdAt", budget.CreatedAt);
            cmd.Parameters.AddWithValue("createdBy", budget.CreatedBy);
            cmd.Parameters.AddWithValue("updatedAt", budget.UpdatedAt);
            cmd.Parameters.AddWithValue("updatedBy", budget.UpdatedBy);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        const string insertVersionSql = @"
            INSERT INTO budget.budget_version (
                org_id, budget_version_id, budget_id, show_id, version_no, version_type,
                total_amount, contingency_amount, status, created_at, created_by, updated_at, updated_by, row_version
            ) VALUES (
                @orgId, @versionId, @budgetId, @showId, 1, 'ORIGINAL',
                0, 0, 'DRAFT', @createdAt, @createdBy, @updatedAt, @updatedBy, 1
            );";

        await using (var cmd = new NpgsqlCommand(insertVersionSql, connection, tx))
        {
            cmd.Parameters.AddWithValue("orgId", version.OrgId);
            cmd.Parameters.AddWithValue("versionId", version.BudgetVersionId);
            cmd.Parameters.AddWithValue("budgetId", version.BudgetId);
            cmd.Parameters.AddWithValue("showId", version.ShowId);
            cmd.Parameters.AddWithValue("createdAt", version.CreatedAt);
            cmd.Parameters.AddWithValue("createdBy", version.CreatedBy);
            cmd.Parameters.AddWithValue("updatedAt", version.UpdatedAt);
            cmd.Parameters.AddWithValue("updatedBy", version.UpdatedBy);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);

        await auditWriter.WriteEventAsync(new AuditEventRecord(
            OrgId: orgId.Value,
            ActorUserId: userId,
            Action: "budget.created",
            EntityType: "budget",
            EntityId: budget.BudgetId,
            Metadata: JsonSerializer.Serialize(new { showId, seasonId, versionId = version.BudgetVersionId })), ct);

        return Results.Created($"/api/v1/shows/{showId}/seasons/{seasonId}/budget", new { budgetId = budget.BudgetId, versionId = version.BudgetVersionId });
    }

    private static async Task<IResult> AddBudgetLineAsync(
        Guid budgetId,
        Guid versionId,
        CreateBudgetLineRequest request,
        NpgsqlDataSource dataSource,
        ITenantContext tenantContext,
        CancellationToken ct)
    {
        var orgId = tenantContext.CurrentOrgId;
        if (!orgId.HasValue) return Results.Unauthorized();

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        // Verify version status is DRAFT
        const string checkSql = "SELECT status, show_id FROM budget.budget_version WHERE budget_version_id = @versionId FOR UPDATE;";
        string? status;
        Guid showId;
        await using (var cmd = new NpgsqlCommand(checkSql, connection, tx))
        {
            cmd.Parameters.AddWithValue("versionId", versionId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                return Results.NotFound(new { error = "ERR-051", message = "Budget version not found." });
            }
            status = reader.GetString(0);
            showId = reader.GetGuid(1);
        }

        if (status != "DRAFT" && status != "CHANGES_REQUESTED")
        {
            return Results.BadRequest(new { error = "ERR-052", message = "Cannot add lines to non-draft budget version." });
        }

        var lineId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        const string insertLineSql = @"
            INSERT INTO budget.budget_line (
                org_id, budget_line_id, budget_version_id, show_id, department_id, category_id,
                cost_type, line_type, amount, quantity, unit, rate, notes, created_at, created_by
            ) VALUES (
                @orgId, @lineId, @versionId, @showId, @deptId, @catId,
                @costType, @lineType, @amount, @qty, @unit, @rate, @notes, now(), @createdBy
            );";

        await using (var cmd = new NpgsqlCommand(insertLineSql, connection, tx))
        {
            cmd.Parameters.AddWithValue("orgId", orgId.Value);
            cmd.Parameters.AddWithValue("lineId", lineId);
            cmd.Parameters.AddWithValue("versionId", versionId);
            cmd.Parameters.AddWithValue("showId", showId);
            cmd.Parameters.AddWithValue("deptId", (object?)request.DepartmentId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("catId", (object?)request.CategoryId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("costType", request.CostType);
            cmd.Parameters.AddWithValue("lineType", request.LineType);
            cmd.Parameters.AddWithValue("amount", request.Amount);
            cmd.Parameters.AddWithValue("qty", (object?)request.Quantity ?? DBNull.Value);
            cmd.Parameters.AddWithValue("unit", (object?)request.Unit ?? DBNull.Value);
            cmd.Parameters.AddWithValue("rate", (object?)request.Rate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("notes", (object?)request.Notes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("createdBy", userId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // Recalculate version total
        const string updateTotalSql = @"
            UPDATE budget.budget_version
            SET total_amount = (SELECT COALESCE(SUM(amount), 0) FROM budget.budget_line WHERE budget_version_id = @versionId) + contingency_amount,
                updated_at = now(),
                row_version = row_version + 1
            WHERE budget_version_id = @versionId;";

        await using (var cmd = new NpgsqlCommand(updateTotalSql, connection, tx))
        {
            cmd.Parameters.AddWithValue("versionId", versionId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        return Results.Created($"/api/v1/budgets/{budgetId}/versions/{versionId}/lines/{lineId}", new { lineId });
    }

    private static async Task<IResult> SubmitBudgetVersionAsync(
        Guid budgetId,
        Guid versionId,
        NpgsqlDataSource dataSource,
        ITenantContext tenantContext,
        IAuditWriter auditWriter,
        CancellationToken ct)
    {
        var orgId = tenantContext.CurrentOrgId;
        if (!orgId.HasValue) return Results.Unauthorized();

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        const string sql = @"
            UPDATE budget.budget_version
            SET status = 'SUBMITTED', submitted_at = now(), updated_at = now(), row_version = row_version + 1
            WHERE budget_version_id = @versionId AND status = 'DRAFT';";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("versionId", versionId);
        int rows = await cmd.ExecuteNonQueryAsync(ct);

        if (rows == 0)
        {
            return Results.BadRequest(new { error = "ERR-053", message = "Budget version must be in DRAFT state to submit." });
        }

        return Results.Ok(new { message = "Budget version submitted for approval." });
    }

    private static async Task<IResult> ApproveBudgetVersionAsync(
        Guid budgetId,
        Guid versionId,
        NpgsqlDataSource dataSource,
        ITenantContext tenantContext,
        IAuditWriter auditWriter,
        IOutboxPublisher outboxPublisher,
        CancellationToken ct)
    {
        var orgId = tenantContext.CurrentOrgId;
        if (!orgId.HasValue) return Results.Unauthorized();

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        // 1. Approve version and mark ACTIVE
        const string approveSql = @"
            UPDATE budget.budget_version
            SET status = 'ACTIVE', approved_at = now(), updated_at = now(), row_version = row_version + 1
            WHERE budget_version_id = @versionId AND status = 'SUBMITTED';";

        await using (var cmd = new NpgsqlCommand(approveSql, connection, tx))
        {
            cmd.Parameters.AddWithValue("versionId", versionId);
            int rows = await cmd.ExecuteNonQueryAsync(ct);
            if (rows == 0)
            {
                return Results.BadRequest(new { error = "ERR-054", message = "Budget version must be in SUBMITTED state to approve." });
            }
        }

        // 2. Baseline container if NOT_BASELINED or update current_version_id
        const string updateContainerSql = @"
            UPDATE budget.budget
            SET status = 'BASELINED', current_version_id = @versionId, updated_at = now(), row_version = row_version + 1
            WHERE budget_id = @budgetId;";

        await using (var cmd = new NpgsqlCommand(updateContainerSql, connection, tx))
        {
            cmd.Parameters.AddWithValue("versionId", versionId);
            cmd.Parameters.AddWithValue("budgetId", budgetId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);

        var userId = Guid.NewGuid();
        await auditWriter.WriteEventAsync(new AuditEventRecord(
            OrgId: orgId.Value,
            ActorUserId: userId,
            Action: "budget_version.approved",
            EntityType: "budget_version",
            EntityId: versionId,
            Metadata: JsonSerializer.Serialize(new { budgetId, versionId })), ct);

        await outboxPublisher.PublishAsync(OutboxEvent.Create(
            aggregateType: "Budget",
            aggregateId: budgetId,
            eventType: "BudgetVersionApproved",
            payload: new { budgetId, versionId, orgId = orgId.Value },
            orgId: orgId.Value), ct);

        return Results.Ok(new { message = "Budget version approved and baselined." });
    }
}
