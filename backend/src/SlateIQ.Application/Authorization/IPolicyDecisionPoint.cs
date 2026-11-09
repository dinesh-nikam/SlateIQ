using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SlateIQ.Domain.Authorization;
using SlateIQ.Domain.Common.ValueObjects;

namespace SlateIQ.Application.Authorization;

public sealed record PolicyEvaluationRequest(
    Guid OrgId,
    Guid UserId,
    PermissionKey RequiredPermission,
    ScopeType TargetScopeType,
    Guid TargetScopeId,
    Guid? ResourceOwnerUserId = null,
    Money? TransactionAmount = null
);

public sealed record PolicyDecision(
    bool IsAllowed,
    string? ErrorCode = null,
    string? DenialReason = null
)
{
    public static PolicyDecision Permit() => new(true);
    public static PolicyDecision Deny(string errorCode, string reason) => new(false, errorCode, reason);
}

public interface IPolicyDecisionPoint
{
    Task<PolicyDecision> EvaluateAsync(PolicyEvaluationRequest request, CancellationToken cancellationToken = default);
}
