using SlateIQ.Domain.Common.Primitives;

namespace SlateIQ.Domain.Budgeting;

public enum BudgetStatus
{
    NOT_BASELINED,
    BASELINED,
    REVISION_IN_PROGRESS,
    FROZEN
}

/// <summary>
/// Root Aggregate for Season Budget Container in SlateIQ (MOD-04).
/// Enforces State Machine SM-03, baselining lock, and container currency.
/// </summary>
public sealed class Budget : Entity<Guid>
{
    public Guid BudgetId => Id;
    public Guid OrgId { get; private set; }
    public Guid ShowId { get; private set; }
    public Guid SeasonId { get; private set; }
    public BudgetStatus Status { get; private set; }
    public Guid? CurrentVersionId { get; private set; }
    public string BaseCurrency { get; private set; } = "INR";
    public DateTimeOffset? FrozenAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public long RowVersion { get; private set; } = 1;

    private Budget() { }

    public static Budget Create(
        Guid orgId,
        Guid showId,
        Guid seasonId,
        Guid createdBy,
        string baseCurrency = "INR")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseCurrency, nameof(baseCurrency));

        var now = DateTimeOffset.UtcNow;
        return new Budget
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            ShowId = showId,
            SeasonId = seasonId,
            Status = BudgetStatus.NOT_BASELINED,
            CurrentVersionId = null,
            BaseCurrency = baseCurrency.ToUpperInvariant(),
            CreatedAt = now,
            CreatedBy = createdBy,
            UpdatedAt = now,
            UpdatedBy = createdBy,
            RowVersion = 1
        };
    }

    /// <summary>
    /// SM-03: NOT_BASELINED -> BASELINED
    /// Sets the initial original active budget version.
    /// </summary>
    public void Baseline(Guid originalVersionId, Guid updatedBy)
    {
        if (Status != BudgetStatus.NOT_BASELINED)
        {
            throw new InvalidOperationException($"Cannot baseline budget in state '{Status}'. Initial baseline requires NOT_BASELINED.");
        }

        Status = BudgetStatus.BASELINED;
        CurrentVersionId = originalVersionId;
        RecordUpdate(updatedBy);
    }

    /// <summary>
    /// SM-03: BASELINED -> REVISION_IN_PROGRESS
    /// </summary>
    public void StartRevision(Guid updatedBy)
    {
        if (Status != BudgetStatus.BASELINED)
        {
            throw new InvalidOperationException($"Cannot start revision on budget in state '{Status}'. Must be BASELINED.");
        }

        Status = BudgetStatus.REVISION_IN_PROGRESS;
        RecordUpdate(updatedBy);
    }

    /// <summary>
    /// SM-03: REVISION_IN_PROGRESS -> BASELINED
    /// Applies an approved revision as the new current active version.
    /// </summary>
    public void ApplyRevision(Guid newApprovedVersionId, Guid updatedBy)
    {
        if (Status != BudgetStatus.REVISION_IN_PROGRESS)
        {
            throw new InvalidOperationException($"Cannot apply revision in state '{Status}'. Must be REVISION_IN_PROGRESS.");
        }

        Status = BudgetStatus.BASELINED;
        CurrentVersionId = newApprovedVersionId;
        RecordUpdate(updatedBy);
    }

    /// <summary>
    /// SM-03: * -> FROZEN
    /// Freezes the budget container upon show closure or lock.
    /// </summary>
    public void Freeze(Guid updatedBy)
    {
        if (Status == BudgetStatus.FROZEN) return;

        Status = BudgetStatus.FROZEN;
        FrozenAt = DateTimeOffset.UtcNow;
        RecordUpdate(updatedBy);
    }

    private void RecordUpdate(Guid updatedBy)
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
        RowVersion++;
    }
}
