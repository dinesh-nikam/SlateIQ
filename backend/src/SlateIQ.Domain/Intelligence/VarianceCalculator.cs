using SlateIQ.Domain.Common.ValueObjects;

namespace SlateIQ.Domain.Intelligence;

public sealed record ProductionFinancialMetrics(
    Money TotalBudget,
    Money TotalActual,
    Money TotalCommitted,
    Money NetVariance,
    decimal BurnRateDaily,
    Money ProjectedFinalCost,
    bool IsOverBudget,
    decimal OverrunPercentage);

/// <summary>
/// Domain Calculation Engine for Production Cost Variance & Projections (MOD-08).
/// Adheres strictly to Document 13 formal measure definitions and scale-4 rounding.
/// </summary>
public static class VarianceCalculator
{
    /// <summary>
    /// Computes canonical financial variance and projections:
    /// NetVariance = TotalBudget - TotalActual - TotalCommitted
    /// </summary>
    public static ProductionFinancialMetrics Calculate(
        Money totalBudget,
        Money totalActual,
        Money totalCommitted,
        int shootDaysElapsed,
        int shootDaysRemaining)
    {
        // Net Variance = Budget - Actual - Committed (INV-002)
        var netVariance = totalBudget - totalActual - totalCommitted;

        decimal burnRateDaily = 0m;
        if (shootDaysElapsed > 0)
        {
            burnRateDaily = Math.Round(totalActual.Amount / shootDaysElapsed, 4, MidpointRounding.AwayFromZero);
        }

        var projectedRemainingCost = Money.From(
            Math.Round(burnRateDaily * Math.Max(0, shootDaysRemaining), 4, MidpointRounding.AwayFromZero),
            totalBudget.Currency);

        var projectedFinalCost = totalActual + projectedRemainingCost;

        bool isOverBudget = projectedFinalCost.Amount > totalBudget.Amount;
        decimal overrunPct = 0m;
        if (totalBudget.Amount > 0 && isOverBudget)
        {
            overrunPct = Math.Round(((projectedFinalCost.Amount - totalBudget.Amount) / totalBudget.Amount) * 100m, 2, MidpointRounding.AwayFromZero);
        }

        return new ProductionFinancialMetrics(
            TotalBudget: totalBudget,
            TotalActual: totalActual,
            TotalCommitted: totalCommitted,
            NetVariance: netVariance,
            BurnRateDaily: burnRateDaily,
            ProjectedFinalCost: projectedFinalCost,
            IsOverBudget: isOverBudget,
            OverrunPercentage: overrunPct);
    }
}
