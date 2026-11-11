using System.Text.Json;
using SlateIQ.Domain.Common.Primitives;

namespace SlateIQ.Domain.Tenancy;

public enum ProvisioningStatus
{
    RUNNING,
    SUCCEEDED,
    FAILED,
    COMPENSATED
}

public sealed record ProvisioningStepRecord(
    int StepNumber,
    string StepName,
    bool Succeeded,
    string Detail,
    DateTimeOffset Timestamp);

/// <summary>
/// Tracks the 10-step idempotent organization provisioning saga (MOD-15 / MOD-01).
/// Adheres to SLQ-ARCH-003 §2.9.1 and §10.1.
/// </summary>
public sealed class ProvisioningRun : Entity<Guid>
{
    public Guid RunId => Id;
    public Guid? OrgId { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public string Region { get; private set; } = "IN";
    public ProvisioningStatus Status { get; private set; }
    public int CurrentStep { get; private set; } = 1;
    public List<ProvisioningStepRecord> StepLog { get; private set; } = [];
    public string IdempotencyKey { get; private set; } = string.Empty;
    public Guid? RequestedBy { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? LastError { get; private set; }

    private ProvisioningRun() { }

    public static ProvisioningRun Start(
        string slug,
        string region,
        string idempotencyKey,
        Guid? requestedBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug, nameof(slug));
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey, nameof(idempotencyKey));

        return new ProvisioningRun
        {
            Id = Guid.NewGuid(),
            Slug = slug.Trim().ToLowerInvariant(),
            Region = region.ToUpperInvariant(),
            Status = ProvisioningStatus.RUNNING,
            CurrentStep = 1,
            IdempotencyKey = idempotencyKey,
            RequestedBy = requestedBy,
            StartedAt = DateTimeOffset.UtcNow,
            StepLog = []
        };
    }

    public void AssociateOrganization(Guid orgId)
    {
        OrgId = orgId;
    }

    public void RecordStepSuccess(int stepNumber, string stepName, string detail)
    {
        StepLog.Add(new ProvisioningStepRecord(stepNumber, stepName, true, detail, DateTimeOffset.UtcNow));
        CurrentStep = Math.Min(stepNumber + 1, 10);
    }

    public void RecordStepFailure(int stepNumber, string stepName, string error)
    {
        LastError = error;
        StepLog.Add(new ProvisioningStepRecord(stepNumber, stepName, false, error, DateTimeOffset.UtcNow));
        Status = ProvisioningStatus.FAILED;
    }

    public void Compensate(string reason)
    {
        Status = ProvisioningStatus.COMPENSATED;
        LastError = reason;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void Complete(Guid orgId)
    {
        OrgId = orgId;
        Status = ProvisioningStatus.SUCCEEDED;
        CompletedAt = DateTimeOffset.UtcNow;
        CurrentStep = 10;
    }
}
