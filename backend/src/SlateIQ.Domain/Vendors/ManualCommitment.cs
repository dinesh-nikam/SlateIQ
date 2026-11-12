using SlateIQ.Domain.Common.Primitives;
using SlateIQ.Domain.Common.ValueObjects;

namespace SlateIQ.Domain.Vendors;

public enum CommitmentType
{
    DEAL_MEMO,
    RENTAL,
    CONTRACT,
    OTHER
}

public enum CommitmentStatus
{
    DRAFT,
    PENDING_APPROVAL,
    OPEN,
    CLOSED,
    CANCELLED,
    EXPIRED
}

/// <summary>
/// Root Aggregate for Manual Commitments / Deal Memos in SlateIQ (MOD-05).
/// Enforces State Machine SM-07, PO limits, and date validities.
/// </summary>
public sealed class ManualCommitment : Entity<Guid>
{
    public Guid CommitmentId => Id;
    public Guid OrgId { get; private set; }
    public string CommitmentNumber { get; private set; } = string.Empty;
    public Guid ShowId { get; private set; }
    public Guid? SeasonId { get; private set; }
    public Guid VendorId { get; private set; }
    public Guid DepartmentId { get; private set; }
    public Guid CategoryId { get; private set; }
    public CommitmentType CommitmentType { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public Money Amount { get; private set; } = Money.Zero("INR");
    public Money? BaseAmount { get; private set; }
    public DateOnly ValidFrom { get; private set; }
    public DateOnly? ValidTo { get; private set; }
    public CommitmentStatus Status { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public string? CloseReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public long RowVersion { get; private set; } = 1;

    private ManualCommitment() { }

    public static ManualCommitment Create(
        Guid orgId,
        string commitmentNumber,
        Guid showId,
        Guid vendorId,
        Guid departmentId,
        Guid categoryId,
        CommitmentType commitmentType,
        string description,
        Money amount,
        DateOnly validFrom,
        Guid createdBy,
        Guid? seasonId = null,
        DateOnly? validTo = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commitmentNumber, nameof(commitmentNumber));
        ArgumentException.ThrowIfNullOrWhiteSpace(description, nameof(description));

        if (amount.IsZero || amount.Amount < 0)
        {
            throw new ArgumentException("Commitment amount must be strictly positive.", nameof(amount));
        }

        if (validTo.HasValue && validTo.Value < validFrom)
        {
            throw new ArgumentException("Valid-to date cannot precede valid-from date.", nameof(validTo));
        }

        var now = DateTimeOffset.UtcNow;
        return new ManualCommitment
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            CommitmentNumber = commitmentNumber.Trim().ToUpperInvariant(),
            ShowId = showId,
            SeasonId = seasonId,
            VendorId = vendorId,
            DepartmentId = departmentId,
            CategoryId = categoryId,
            CommitmentType = commitmentType,
            Description = description.Trim(),
            Amount = amount,
            BaseAmount = amount, // Default 1:1 if base currency
            ValidFrom = validFrom,
            ValidTo = validTo,
            Status = CommitmentStatus.DRAFT,
            CreatedAt = now,
            CreatedBy = createdBy,
            UpdatedAt = now,
            UpdatedBy = createdBy,
            RowVersion = 1
        };
    }

    /// <summary>
    /// SM-07: DRAFT -> PENDING_APPROVAL
    /// </summary>
    public void SubmitForApproval(Guid updatedBy)
    {
        if (Status != CommitmentStatus.DRAFT)
        {
            throw new InvalidOperationException($"Cannot submit commitment in state '{Status}'. Must be DRAFT.");
        }

        Status = CommitmentStatus.PENDING_APPROVAL;
        RecordUpdate(updatedBy);
    }

    /// <summary>
    /// SM-07: PENDING_APPROVAL -> OPEN
    /// </summary>
    public void Approve(Guid approvedBy)
    {
        if (Status != CommitmentStatus.PENDING_APPROVAL)
        {
            throw new InvalidOperationException($"Cannot approve commitment in state '{Status}'. Must be PENDING_APPROVAL.");
        }

        Status = CommitmentStatus.OPEN;
        RecordUpdate(approvedBy);
    }

    /// <summary>
    /// SM-07: OPEN -> CLOSED
    /// </summary>
    public void Close(string closeReason, Guid updatedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(closeReason, nameof(closeReason));

        if (Status != CommitmentStatus.OPEN)
        {
            throw new InvalidOperationException($"Cannot close commitment in state '{Status}'. Must be OPEN.");
        }

        Status = CommitmentStatus.CLOSED;
        ClosedAt = DateTimeOffset.UtcNow;
        CloseReason = closeReason.Trim();
        RecordUpdate(updatedBy);
    }

    /// <summary>
    /// SM-07: DRAFT/PENDING_APPROVAL -> CANCELLED
    /// </summary>
    public void Cancel(Guid updatedBy)
    {
        if (Status != CommitmentStatus.DRAFT && Status != CommitmentStatus.PENDING_APPROVAL)
        {
            throw new InvalidOperationException($"Cannot cancel commitment in state '{Status}'. Only pre-open commitments can be cancelled.");
        }

        Status = CommitmentStatus.CANCELLED;
        RecordUpdate(updatedBy);
    }

    private void RecordUpdate(Guid updatedBy)
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
        RowVersion++;
    }
}
