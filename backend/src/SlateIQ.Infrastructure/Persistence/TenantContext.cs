using System;
using System.Threading;
using SlateIQ.Application.Abstractions.Persistence;

namespace SlateIQ.Infrastructure.Persistence;

public sealed class TenantContext : ITenantContext
{
    private static readonly AsyncLocal<Guid?> _currentOrgId = new();
    private static readonly AsyncLocal<string?> _correlationId = new();

    public Guid? CurrentOrgId => _currentOrgId.Value;
    public string? CorrelationId => _correlationId.Value;

    public void SetTenantContext(Guid orgId, string? correlationId = null)
    {
        _currentOrgId.Value = orgId;
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            _correlationId.Value = correlationId;
        }
    }

    public static void Clear()
    {
        _currentOrgId.Value = null;
        _correlationId.Value = null;
    }
}
