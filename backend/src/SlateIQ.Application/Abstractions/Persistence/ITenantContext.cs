using System;

namespace SlateIQ.Application.Abstractions.Persistence;

public interface ITenantContext
{
    Guid? CurrentOrgId { get; }
    string? CorrelationId { get; }
    bool IsTenantSet => CurrentOrgId.HasValue && CurrentOrgId.Value != Guid.Empty;
    void SetTenantContext(Guid orgId, string? correlationId = null);
}
