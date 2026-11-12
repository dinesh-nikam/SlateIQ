using FluentAssertions;
using SlateIQ.Domain.Common.ValueObjects;
using SlateIQ.Domain.Intelligence;
using Xunit;

namespace SlateIQ.Domain.Tests.Intelligence;

public class VarianceCalculatorTests
{
    [Fact]
    public void Calculate_WithHealthyBudget_ReturnsPositiveVarianceAndNoOverrun()
    {
        var budget = Money.From(10000000.0000m, "INR");
        var actual = Money.From(4000000.0000m, "INR");
        var committed = Money.From(2000000.0000m, "INR");

        // 20 days elapsed, 30 days remaining
        var metrics = VarianceCalculator.Calculate(budget, actual, committed, 20, 30);

        metrics.NetVariance.Amount.Should().Be(4000000.0000m); // 10M - 4M - 2M = 4M
        metrics.BurnRateDaily.Should().Be(200000.0000m); // 4M / 20 = 200k/day
        metrics.ProjectedFinalCost.Amount.Should().Be(10000000.0000m); // 4M + (200k * 30) = 10M
        metrics.IsOverBudget.Should().BeFalse();
        metrics.OverrunPercentage.Should().Be(0m);
    }

    [Fact]
    public void Calculate_WithHighBurnRate_DetectsProjectedCostOverrun()
    {
        var budget = Money.From(5000000.0000m, "INR");
        var actual = Money.From(3000000.0000m, "INR");
        var committed = Money.From(1000000.0000m, "INR");

        // 10 days elapsed (300k/day), 15 days remaining -> Projected: 3M + 4.5M = 7.5M (50% overrun)
        var metrics = VarianceCalculator.Calculate(budget, actual, committed, 10, 15);

        metrics.NetVariance.Amount.Should().Be(1000000.0000m);
        metrics.BurnRateDaily.Should().Be(300000.0000m);
        metrics.ProjectedFinalCost.Amount.Should().Be(7500000.0000m);
        metrics.IsOverBudget.Should().BeTrue();
        metrics.OverrunPercentage.Should().Be(50.00m);
    }
}
