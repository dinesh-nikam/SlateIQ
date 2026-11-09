using System;
using System.Linq;
using FluentAssertions;
using SlateIQ.Domain.Common.ValueObjects;
using Xunit;

namespace SlateIQ.Domain.Tests.Common.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Money_ShouldRoundHalfUp_AwayFromZero()
    {
        // 12.34565 rounds to 12.3457 (AwayFromZero)
        var m1 = new Money(12.34565m, "INR");
        m1.Amount.Should().Be(12.3457m);

        // -12.34565 rounds to -12.3457 (AwayFromZero)
        var m2 = new Money(-12.34565m, "INR");
        m2.Amount.Should().Be(-12.3457m);
    }

    [Fact]
    public void Money_Addition_SameCurrency_Succeeds()
    {
        var m1 = new Money(100.5000m, "INR");
        var m2 = new Money(50.2500m, "INR");

        var sum = m1 + m2;

        sum.Amount.Should().Be(150.7500m);
        sum.Currency.Should().Be("INR");
    }

    [Fact]
    public void Money_Addition_DifferentCurrency_ThrowsInvalidOperationException()
    {
        var m1 = new Money(100m, "INR");
        var m2 = new Money(100m, "USD");

        Action act = () => _ = m1 + m2;

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*different currencies*");
    }

    [Fact]
    public void Money_MultiplyByRate_MaintainsStoredScale4()
    {
        var m = new Money(100.0000m, "USD");
        decimal rate = 84.125678m; // Scale 6

        var converted = m * rate;

        converted.Amount.Should().Be(8412.5678m);
        converted.Currency.Should().Be("USD");
    }

    [Fact]
    public void Money_Allocate_ConservesExactTotal_Invariant_INV002()
    {
        // Allocate 100.0000 across 3 equal parts (100 / 3 = 33.3333 with 0.0001 remainder)
        var total = new Money(100.0000m, "INR");
        int[] ratios = [1, 1, 1];

        var splits = total.Allocate(ratios);

        splits.Should().HaveCount(3);
        decimal sumSplits = splits.Sum(s => s.Amount);

        // Invariant INV-002: sum of splits MUST exactly equal the original total
        sumSplits.Should().Be(total.Amount);
        splits[0].Amount.Should().Be(33.3334m); // First share receives remaining fraction
        splits[1].Amount.Should().Be(33.3333m);
        splits[2].Amount.Should().Be(33.3333m);
    }
}
