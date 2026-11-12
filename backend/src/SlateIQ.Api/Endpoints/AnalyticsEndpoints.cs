using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Npgsql;
using SlateIQ.Application.Abstractions.Persistence;
using SlateIQ.Domain.Common.ValueObjects;
using SlateIQ.Domain.Intelligence;

namespace SlateIQ.Api.Endpoints;

public static class AnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapAnalyticsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1");

        group.MapGet("/shows/{showId:guid}/variance", GetShowVarianceAsync);
        group.MapGet("/alerts", ListAlertsAsync);

        return endpoints;
    }

    private static async Task<IResult> GetShowVarianceAsync(
        Guid showId,
        NpgsqlDataSource dataSource,
        ITenantContext tenantContext,
        CancellationToken ct)
    {
        var orgId = tenantContext.CurrentOrgId;
        if (!orgId.HasValue) return Results.Unauthorized();

        await using var connection = await dataSource.OpenConnectionAsync(ct);

        // Fetch Total Budget from active budget version
        const string budgetSql = @"
            SELECT COALESCE(bv.total_amount, 0), b.base_currency
            FROM budget.budget b
            LEFT JOIN budget.budget_version bv ON b.current_version_id = bv.budget_version_id
            WHERE b.show_id = @showId;";

        decimal budgetAmount = 0m;
        string currency = "INR";

        await using (var cmd = new NpgsqlCommand(budgetSql, connection))
        {
            cmd.Parameters.AddWithValue("showId", showId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                budgetAmount = reader.GetDecimal(0);
                currency = reader.GetString(1);
            }
        }

        // Fetch Total Actual from cost postings
        const string actualSql = @"
            SELECT COALESCE(SUM(amount), 0)
            FROM expense.cost_posting
            WHERE show_id = @showId;";

        decimal actualAmount = 0m;
        await using (var cmd = new NpgsqlCommand(actualSql, connection))
        {
            cmd.Parameters.AddWithValue("showId", showId);
            actualAmount = Convert.ToDecimal(await cmd.ExecuteScalarAsync(ct));
        }

        // Fetch Total Committed from open manual commitments
        const string committedSql = @"
            SELECT COALESCE(SUM(amount), 0)
            FROM vendor.manual_commitment
            WHERE show_id = @showId AND status = 'OPEN';";

        decimal committedAmount = 0m;
        await using (var cmd = new NpgsqlCommand(committedSql, connection))
        {
            cmd.Parameters.AddWithValue("showId", showId);
            committedAmount = Convert.ToDecimal(await cmd.ExecuteScalarAsync(ct));
        }

        var metrics = VarianceCalculator.Calculate(
            Money.From(budgetAmount, currency),
            Money.From(actualAmount, currency),
            Money.From(committedAmount, currency),
            shootDaysElapsed: 15,
            shootDaysRemaining: 35);

        return Results.Ok(new
        {
            showId,
            totalBudget = metrics.TotalBudget.Amount,
            totalActual = metrics.TotalActual.Amount,
            totalCommitted = metrics.TotalCommitted.Amount,
            netVariance = metrics.NetVariance.Amount,
            burnRateDaily = metrics.BurnRateDaily,
            projectedFinalCost = metrics.ProjectedFinalCost.Amount,
            isOverBudget = metrics.IsOverBudget,
            overrunPercentage = metrics.OverrunPercentage,
            currency
        });
    }

    private static async Task<IResult> ListAlertsAsync(
        NpgsqlDataSource dataSource,
        ITenantContext tenantContext,
        CancellationToken ct)
    {
        var orgId = tenantContext.CurrentOrgId;
        if (!orgId.HasValue) return Results.Unauthorized();

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        const string sql = @"
            SELECT alert_id, show_id, title, message, current_variance_amount, severity, status, triggered_at
            FROM intel.alert_instance
            ORDER BY triggered_at DESC;";

        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var list = new List<object>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new
            {
                AlertId = reader.GetGuid(0),
                ShowId = reader.GetGuid(1),
                Title = reader.GetString(2),
                Message = reader.GetString(3),
                CurrentVarianceAmount = reader.GetDecimal(4),
                Severity = reader.GetString(5),
                Status = reader.GetString(6),
                TriggeredAt = reader.GetDateTime(7)
            });
        }

        return Results.Ok(list);
    }
}
