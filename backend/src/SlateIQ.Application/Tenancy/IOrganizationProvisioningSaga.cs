using SlateIQ.Domain.Tenancy;

namespace SlateIQ.Application.Tenancy;

public sealed record ProvisionOrganizationRequest(
    string Slug,
    string LegalName,
    OrganizationType OrgType,
    string BaseCurrency,
    OrganizationTier Tier,
    string HomeRegion,
    string AdminEmail,
    string AdminFullName,
    string IdempotencyKey,
    Guid? RequestedBy = null);

public sealed record ProvisionOrganizationResult(
    Guid RunId,
    Guid? OrgId,
    string Slug,
    ProvisioningStatus Status,
    int CurrentStep,
    IReadOnlyList<ProvisioningStepRecord> StepLog,
    string? AdminInvitationToken = null,
    string? Error = null);

/// <summary>
/// Orchestrates the 10-step idempotent organization provisioning saga (MOD-15 / MOD-01).
/// Adheres to SLQ-ARCH-003 §2.9.1 and §10.1.
/// </summary>
public interface IOrganizationProvisioningSaga
{
    Task<ProvisionOrganizationResult> ExecuteAsync(
        ProvisionOrganizationRequest request,
        CancellationToken cancellationToken = default);

    Task<ProvisioningRun?> GetRunStatusAsync(
        Guid runId,
        CancellationToken cancellationToken = default);
}
