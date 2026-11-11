using SlateIQ.Domain.Common.Primitives;

namespace SlateIQ.Domain.Tenancy;

public enum OrganizationType
{
    OTT_PLATFORM,
    BROADCASTER,
    PRODUCTION_HOUSE,
    AUDIT_FIRM,
    STUDIO
}

public enum TenantStatus
{
    PROVISIONING,
    PROVISIONING_FAILED,
    ACTIVE,
    SUSPENDED,
    ARCHIVED,
    PENDING_DELETION,
    PURGING,
    PURGED
}

public enum OrganizationTier
{
    PILOT,
    STANDARD,
    ENTERPRISE,
    DEDICATED
}

/// <summary>
/// Root Aggregate for Tenant / Organization in SlateIQ (MOD-01).
/// Adheres to SM-01 State Machine and forced Row-Level Security isolation.
/// </summary>
public sealed class Organization : Entity<Guid>
{
    public string Code { get; private set; } = string.Empty;
    public string LegalName { get; private set; } = string.Empty;
    public OrganizationType OrgType { get; private set; }
    public TenantStatus Status { get; private set; }
    public string BaseCurrency { get; private set; } = "INR";
    public OrganizationTier Tier { get; private set; }
    public string HomeRegion { get; private set; } = "IN";
    public string SettingsJson { get; private set; } = "{}";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public int Version { get; private set; } = 1;

    private Organization() { }

    public static Organization CreateProvisioning(
        string code,
        string legalName,
        OrganizationType orgType,
        string baseCurrency = "INR",
        OrganizationTier tier = OrganizationTier.STANDARD,
        string homeRegion = "IN")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code, nameof(code));
        ArgumentException.ThrowIfNullOrWhiteSpace(legalName, nameof(legalName));

        var normalizedCode = code.Trim().ToLowerInvariant();
        if (normalizedCode.Length < 3 || normalizedCode.Length > 50)
        {
            throw new ArgumentException("Organization slug must be between 3 and 50 characters", nameof(code));
        }

        if (homeRegion != "IN" && homeRegion != "EU")
        {
            throw new ArgumentException("Home region must be IN or EU per residency requirements", nameof(homeRegion));
        }

        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Code = normalizedCode,
            LegalName = legalName.Trim(),
            OrgType = orgType,
            Status = TenantStatus.PROVISIONING,
            BaseCurrency = baseCurrency.ToUpperInvariant(),
            Tier = tier,
            HomeRegion = homeRegion,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 1
        };

        return org;
    }

    /// <summary>
    /// SM-01: Transitions organization from PROVISIONING to ACTIVE after isolation probe passes.
    /// </summary>
    public void Activate()
    {
        if (Status != TenantStatus.PROVISIONING && Status != TenantStatus.PROVISIONING_FAILED)
        {
            throw new InvalidOperationException($"Cannot activate tenant in state '{Status}'. Valid transitions are from PROVISIONING.");
        }

        Status = TenantStatus.ACTIVE;
        UpdatedAt = DateTimeOffset.UtcNow;
        Version++;
    }

    /// <summary>
    /// SM-01: Transitions organization to PROVISIONING_FAILED upon step compensation.
    /// </summary>
    public void MarkProvisioningFailed()
    {
        if (Status != TenantStatus.PROVISIONING)
        {
            throw new InvalidOperationException($"Cannot fail provisioning from state '{Status}'.");
        }

        Status = TenantStatus.PROVISIONING_FAILED;
        UpdatedAt = DateTimeOffset.UtcNow;
        Version++;
    }

    /// <summary>
    /// SM-01: Suspends active organization with an operational reason code.
    /// </summary>
    public void Suspend(string reasonCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode, nameof(reasonCode));

        if (Status != TenantStatus.ACTIVE)
        {
            throw new InvalidOperationException($"Cannot suspend tenant in state '{Status}'.");
        }

        Status = TenantStatus.SUSPENDED;
        UpdatedAt = DateTimeOffset.UtcNow;
        Version++;
    }

    /// <summary>
    /// SM-01: Reactivates suspended organization.
    /// </summary>
    public void Reactivate()
    {
        if (Status != TenantStatus.SUSPENDED)
        {
            throw new InvalidOperationException($"Cannot reactivate tenant in state '{Status}'.");
        }

        Status = TenantStatus.ACTIVE;
        UpdatedAt = DateTimeOffset.UtcNow;
        Version++;
    }
}
