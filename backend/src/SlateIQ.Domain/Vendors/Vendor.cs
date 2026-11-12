using SlateIQ.Domain.Common.Primitives;

namespace SlateIQ.Domain.Vendors;

public enum VendorType
{
    INDIVIDUAL,
    SOLE_PROPRIETOR,
    PARTNERSHIP,
    COMPANY,
    GOVERNMENT,
    OTHER
}

public enum VendorStatus
{
    CANDIDATE,
    ACTIVE,
    INACTIVE,
    BLOCKED,
    REJECTED,
    MERGED
}

public enum RiskRating
{
    LOW,
    MEDIUM,
    HIGH,
    UNRATED
}

/// <summary>
/// Root Aggregate for Vendor / Supplier Master in SlateIQ (MOD-05).
/// Enforces State Machine SM-06 and vendor invariants.
/// </summary>
public sealed class Vendor : Entity<Guid>
{
    public Guid VendorId => Id;
    public Guid OrgId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string LegalName { get; private set; } = string.Empty;
    public string NormalizedLegalName { get; private set; } = string.Empty;
    public string? TradeName { get; private set; }
    public VendorType VendorType { get; private set; }
    public string? PrimaryTaxIdentifier { get; private set; }
    public VendorStatus Status { get; private set; }
    public RiskRating RiskRating { get; private set; } = RiskRating.UNRATED;
    public string? BlockedReason { get; private set; }
    public Guid? MergedIntoVendorId { get; private set; }
    public string CreatedVia { get; private set; } = "MASTER";
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public long RowVersion { get; private set; } = 1;

    private Vendor() { }

    public static Vendor Create(
        Guid orgId,
        string code,
        string legalName,
        VendorType vendorType,
        Guid createdBy,
        string? tradeName = null,
        string? primaryTaxIdentifier = null,
        string createdVia = "MASTER")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code, nameof(code));
        ArgumentException.ThrowIfNullOrWhiteSpace(legalName, nameof(legalName));

        var normalized = legalName.Trim().ToUpperInvariant();
        var now = DateTimeOffset.UtcNow;

        return new Vendor
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Code = code.Trim().ToUpperInvariant(),
            LegalName = legalName.Trim(),
            NormalizedLegalName = normalized,
            TradeName = tradeName?.Trim(),
            VendorType = vendorType,
            PrimaryTaxIdentifier = primaryTaxIdentifier?.Trim().ToUpperInvariant(),
            Status = VendorStatus.CANDIDATE,
            RiskRating = RiskRating.UNRATED,
            CreatedVia = createdVia,
            CreatedAt = now,
            CreatedBy = createdBy,
            UpdatedAt = now,
            UpdatedBy = createdBy,
            RowVersion = 1
        };
    }

    /// <summary>
    /// SM-06: CANDIDATE -> ACTIVE
    /// </summary>
    public void Approve(Guid updatedBy)
    {
        if (Status != VendorStatus.CANDIDATE)
        {
            throw new InvalidOperationException($"Cannot approve vendor in state '{Status}'. Must be CANDIDATE.");
        }

        Status = VendorStatus.ACTIVE;
        RecordUpdate(updatedBy);
    }

    /// <summary>
    /// SM-06: ACTIVE -> BLOCKED
    /// </summary>
    public void Block(string reason, Guid updatedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason, nameof(reason));

        if (Status != VendorStatus.ACTIVE)
        {
            throw new InvalidOperationException($"Cannot block vendor in state '{Status}'. Must be ACTIVE.");
        }

        Status = VendorStatus.BLOCKED;
        BlockedReason = reason.Trim();
        RecordUpdate(updatedBy);
    }

    /// <summary>
    /// SM-06: ACTIVE -> INACTIVE
    /// </summary>
    public void Inactivate(Guid updatedBy)
    {
        if (Status != VendorStatus.ACTIVE)
        {
            throw new InvalidOperationException($"Cannot inactivate vendor in state '{Status}'. Must be ACTIVE.");
        }

        Status = VendorStatus.INACTIVE;
        RecordUpdate(updatedBy);
    }

    /// <summary>
    /// SM-06: INACTIVE/BLOCKED -> ACTIVE
    /// </summary>
    public void Reactivate(Guid updatedBy)
    {
        if (Status != VendorStatus.INACTIVE && Status != VendorStatus.BLOCKED)
        {
            throw new InvalidOperationException($"Cannot reactivate vendor in state '{Status}'. Must be INACTIVE or BLOCKED.");
        }

        Status = VendorStatus.ACTIVE;
        BlockedReason = null;
        RecordUpdate(updatedBy);
    }

    /// <summary>
    /// SM-06: * -> MERGED (Irreversible, BR-VND-006)
    /// </summary>
    public void MergeInto(Guid targetVendorId, Guid updatedBy)
    {
        if (targetVendorId == Id)
        {
            throw new ArgumentException("Cannot merge vendor into itself.", nameof(targetVendorId));
        }

        if (Status == VendorStatus.MERGED)
        {
            throw new InvalidOperationException("Vendor is already merged.");
        }

        Status = VendorStatus.MERGED;
        MergedIntoVendorId = targetVendorId;
        RecordUpdate(updatedBy);
    }

    private void RecordUpdate(Guid updatedBy)
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
        RowVersion++;
    }
}
