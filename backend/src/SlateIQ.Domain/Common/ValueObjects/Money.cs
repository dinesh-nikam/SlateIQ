using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace SlateIQ.Domain.Common.ValueObjects;

/// <summary>
/// Immutable monetary value object enforcing fixed-point arithmetic,
/// away-from-zero half-up rounding (RC-01, BR-FIN-002), and scale-4 precision (ADR-037).
/// </summary>
public readonly struct Money : IEquatable<Money>, IComparable<Money>
{
    public const int StoredScale = 4;
    public const MidpointRounding StandardRounding = MidpointRounding.AwayFromZero;

    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
        {
            throw new ArgumentException("Currency must be a valid 3-letter ISO code.", nameof(currency));
        }

        Currency = currency.Trim().ToUpperInvariant();
        Amount = Math.Round(amount, StoredScale, StandardRounding);
    }

    public static Money Zero(string currency) => new(0m, currency);

    public static Money operator +(Money left, Money right)
    {
        AssertSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        AssertSameCurrency(left, right);
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    public static Money operator *(Money money, decimal rate)
    {
        // Rate is scaled at numeric(19,6) and rounded to scale 4
        decimal result = Math.Round(money.Amount * rate, StoredScale, StandardRounding);
        return new Money(result, money.Currency);
    }

    public static Money operator -(Money money) => new(-money.Amount, money.Currency);

    public static bool operator ==(Money left, Money right) => left.Equals(right);
    public static bool operator !=(Money left, Money right) => !left.Equals(right);
    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;
    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;
    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;
    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    /// <summary>
    /// Allocates the money according to specific ratios while conserving the exact total (INV-002).
    /// Any fractional remainder is allocated sequentially to avoid rounding leakage.
    /// </summary>
    public IReadOnlyList<Money> Allocate(int[] ratios)
    {
        if (ratios == null || ratios.Length == 0)
            throw new ArgumentException("Allocation ratios cannot be empty.", nameof(ratios));

        int totalWeight = 0;
        foreach (int r in ratios)
        {
            if (r < 0) throw new ArgumentException("Ratios cannot be negative.", nameof(ratios));
            totalWeight += r;
        }

        if (totalWeight == 0)
            throw new ArgumentException("Total ratio weight cannot be zero.", nameof(ratios));

        var results = new Money[ratios.Length];
        decimal remainder = Amount;

        for (int i = 0; i < ratios.Length; i++)
        {
            decimal share = Math.Round(Amount * ratios[i] / totalWeight, StoredScale, StandardRounding);
            results[i] = new Money(share, Currency);
            remainder -= share;
        }

        // Distribute remainder cent-by-cent to enforce exact conservation
        if (remainder != 0m)
        {
            decimal step = remainder > 0 ? 0.0001m : -0.0001m;
            int idx = 0;
            while (remainder != 0m)
            {
                results[idx] = new Money(results[idx].Amount + step, Currency);
                remainder -= step;
                idx = (idx + 1) % results.Length;
            }
        }

        return results;
    }

    private static void AssertSameCurrency(Money a, Money b)
    {
        if (!string.Equals(a.Currency, b.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Cannot perform arithmetic on different currencies: {a.Currency} vs {b.Currency}");
        }
    }

    public bool Equals(Money other) => Amount == other.Amount && Currency == other.Currency;
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Money other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Amount, Currency);
    public int CompareTo(Money other)
    {
        AssertSameCurrency(this, other);
        return Amount.CompareTo(other.Amount);
    }

    public override string ToString() => $"{Amount:F4} {Currency}";
}
