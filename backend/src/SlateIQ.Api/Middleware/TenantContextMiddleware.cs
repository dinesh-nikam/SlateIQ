using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using SlateIQ.Application.Abstractions.Persistence;

namespace SlateIQ.Api.Middleware;

public sealed class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        // 1. Correlation ID management
        if (!context.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId) ||
            string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }
        context.Response.Headers["X-Correlation-ID"] = correlationId;

        // 2. Tenant ID resolution from Header or JWT Claim
        Guid? orgId = null;
        if (context.Request.Headers.TryGetValue("X-Tenant-ID", out var tenantHeader) &&
            Guid.TryParse(tenantHeader, out var parsedOrgId))
        {
            orgId = parsedOrgId;
        }
        else if (context.User.Identity?.IsAuthenticated == true)
        {
            var claim = context.User.FindFirst("tenant_id") ?? context.User.FindFirst("org_id");
            if (claim != null && Guid.TryParse(claim.Value, out var claimOrgId))
            {
                orgId = claimOrgId;
            }
        }

        if (orgId.HasValue)
        {
            tenantContext.SetTenantContext(orgId.Value, correlationId);
        }

        await _next(context);
    }
}
