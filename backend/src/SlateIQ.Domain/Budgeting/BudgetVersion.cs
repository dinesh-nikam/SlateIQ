using SlateIQ.Domain.Common.Primitives;
using SlateIQ.Domain.Common.ValueObjects;

namespace SlateIQ.Domain.Budgeting;

public enum BudgetVersionType
{
    ORIGINAL,
    REVISION
}

public enum BudgetVersionStatus
{
    DRAFT,
    SUBMITTED,
    CHANGES_REQUESTED,
    ACTIVE,
    SUPERSEDED,
    REJECTED,
    WITHDRAWN
}

/// <summary>
/// Immutable budget plan version (SM-04).
/// Enforces BR-BUD-001 (single active version), BR-BUD-003 (sum of lines equals total),
/// and post-draft immutability.
/// </summary>
public sealed class BudgetVersion : Entity<Guid>
{
    private readonly List<BudgetLine> _lines = [];

    public Guid BudgetVersionId => Id;
    public Guid OrgId { get; private set; }
    public Guid BudgetId { get; private set; }
    public Guid ShowId { get; private set; }
    public int VersionNo { get; private set; }
    public BudgetVersionType VersionType { get; private set; }
    public string? Reason { get; private set; }
    public DateOnly? EffectiveDate { get; private set; }
    public Money TotalAmount { get; private set; } = Money.Zero("INR");
    public Money ContingencyAmount { get; private set; } = Money.Zero("INR");
    public BudgetVersionStatus Status { get; private set; }
    public Guid? SupersedesVersionId { get; private set; }
    public Guid? SubmittedBy { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public long RowVersion { get; private set; } = 1;

    public IReadOnlyList<BudgetLine> Lines => _lines.AsReadOnly();

    private BudgetVersion() { }

    public static BudgetVersion CreateOriginalDraft(
        Guid orgId,
        Guid budgetId,
        Guid showId,
        string currency,
        Guid createdBy)
    {
        var now = DateTimeOffset.UtcNow;
        return new BudgetVersion
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            BudgetId = budgetId,
            ShowId = showId,
            VersionNo = 1,
            VersionType = BudgetVersionType.ORIGINAL,
            TotalAmount = Money.Zero(currency),
            ContingencyAmount = Money.Zero(currency),
            Status = BudgetVersionStatus.DRAFT,
            CreatedAt = now,
            CreatedBy = createdBy,
            UpdatedAt = now,
            UpdatedBy = createdBy,
            RowVersion = 1
        };
    }

    public static BudgetVersion CreateRevisionDraft(
        Guid orgId,
        Guid budgetId,
        Guid showId,
        int newVersionNo,
        Guid supersedesVersionId,
        string reason,
        DateOnly effectiveDate,
        string currency,
        Guid createdBy)
    {
        if (newVersionNo <= 1)
        {
            throw new ArgumentException("Revision version number must be greater than 1.", nameof(newVersionNo));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reason, nameof(reason));

        var now = DateTimeOffset.UtcNow;
        return new BudgetVersion
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            BudgetId = budgetId,
            ShowId = showId,
            VersionNo = newVersionNo,
            VersionType = BudgetVersionType.REVISION,
            Reason = reason.Trim(),
            EffectiveDate = effectiveDate,
            SupersedesVersionId = supersedesVersionId,
            TotalAmount = Money.Zero(currency),
            ContingencyAmount = Money.Zero(currency),
            Status = BudgetVersionStatus.DRAFT,
            CreatedAt = now,
            CreatedBy = createdBy,
            UpdatedAt = now,
            UpdatedBy = createdBy,
            RowVersion = 1
        };
    }

    /// <summary>
    /// Adds or updates an operational budget line item in DRAFT state.
    /// </summary>
    public void AddLine(BudgetLine line)
    {
        AssertMutable("add lines to");
        _lines.Add(line);
        RecalculateTotal();
    }

    /// <summary>
    /// Sets contingency allocation in DRAFT state.
    /// </summary>
    public void SetContingency(Money contingency)
    {
        AssertMutable("modify contingency on");
        ContingencyAmount = contingency;
        RecalculateTotal();
    }

    /// <summary>
    /// SM-04: DRAFT -> SUBMITTED
    /// </summary>
    public void Submit(Guid submittedBy)
    {
        if (Status != BudgetVersionStatus.DRAFT && Status != BudgetVersionStatus.CHANGES_REQUESTED)
        {
            throw new InvalidOperationException($"Cannot submit budget version in state '{Status}'.");
        }

        if (_lines.Count == 0 && ContingencyAmount.IsZero)
        {
            throw new InvalidOperationException("Cannot submit an empty budget version without lines or contingency.");
        }

        ValidateLineSumConservation();

        Status = BudgetVersionStatus.SUBMITTED;
        SubmittedBy = submittedBy;
        SubmittedAt = DateTimeOffset.UtcNow;
        RecordUpdate(submittedBy);
    }

    /// <summary>
    /// SM-04: SUBMITTED -> ACTIVE (Approved)
    /// </summary>
    public void Approve(Guid approvedBy)
    {
        if (Status != BudgetVersionStatus.SUBMITTED)
        {
            throw new InvalidOperationException($"Cannot approve budget version in state '{Status}'. Must be SUBMITTED.");
        }

        Status = BudgetVersionStatus.ACTIVE;
        ApprovedBy = approvedBy;
        ApprovedAt = DateTimeOffset.UtcNow;
        RecordUpdate(approvedBy);
    }

    /// <summary>
    /// SM-04: ACTIVE -> SUPERSEDED
    /// </summary>
    public void Supersede(Guid updatedBy)
    {
        if (Status != BudgetVersionStatus.ACTIVE)
        {
            throw new InvalidOperationException($"Cannot supersede budget version in state '{Status}'. Must be ACTIVE.");
        }

        Status = BudgetVersionStatus.SUPERSEDED;
        RecordUpdate(updatedBy);
    }

    private void RecalculateTotal()
    {
        var lineSum = _lines.Aggregate(Money.Zero(TotalAmount.Currency), (acc, l) => acc + l.Amount);
        TotalAmount = lineSum + ContingencyAmount;
    }

    private void ValidateLineSumConservation()
    {
        var calculatedSum = _lines.Aggregate(Money.Zero(TotalAmount.Currency), (acc, l) => acc + l.Amount) + ContingencyAmount;
        if (calculatedSum != TotalAmount)
        {
            throw new InvalidOperationException($"Conservation invariant violation (INV-002, BR-BUD-003): Sum of lines ({calculatedSum.Amount}) does not equal total ({TotalAmount.Amount}).");
        }
    }

    private void AssertMutable(string action)
    {
        if (Status != BudgetVersionStatus.DRAFT && Status != BudgetVersionStatus.CHANGES_REQUESTED)
        {
            throw new InvalidOperationException($"Immutability invariant violation (BR-BUD-003): Cannot {action} budget version in state '{Status}'. Versions outside DRAFT are immutable.");
        }
    }

    private void RecordUpdate(Guid updatedBy)
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
        RowVersion++;
    }
}

public sealed class BudgetLine : Entity<Guid>
{
    public Guid BudgetLineId => Id;
    public Guid OrgId { get; private set; }
    public Guid BudgetVersionId { get; private set; }
    public Guid ShowId { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public Guid? CategoryId { get; private set; }
    public string CostType { get; private set; }
    public string LineType { get; private set; } = "OPERATIONAL";
    public Money Amount { get; private set; }
    public decimal? Quantity { get; private set; }
    public string? Unit { get; private set; }
    public Money? Rate { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }

    private BudgetLine() { }

    public static BudgetLine Create(
        Guid orgId,
        Guid budgetVersionId,
        Guid showId,
        Guid? departmentId,
        Guid? categoryId,
        string costType,
        Money amount,
        Guid createdBy,
        string lineType = "OPERATIONAL",
        decimal? quantity = null,
        string? unit = null,
        Money? rate = null,
        string? notes = null)
    {
        if (lineType != "CONTINGENCY" && (departmentId == null || categoryId == null))
        {
            throw new ArgumentException("Operational budget line requires department and cost category references.", nameof(departmentId));
        }

        return new BudgetLine
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            BudgetVersionId = budgetVersionId,
            ShowId = showId,
            DepartmentId = departmentId,
            CategoryId = categoryId,
            CostType = costType,
            LineType = lineType,
            Amount = amount,
            Quantity = quantity,
            Unit = unit,
            Rate = rate,
            Notes = notes,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = createdBy
        };
    }
}
