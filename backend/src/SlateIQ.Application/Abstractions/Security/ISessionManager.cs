using System;
using System.Threading;
using System.Threading.Tasks;

namespace SlateIQ.Application.Abstractions.Security;

public sealed record RefreshSessionResult(
    bool IsSuccess,
    string? NewRefreshToken = null,
    Guid? UserId = null,
    Guid? OrgId = null,
    string? ErrorCode = null
);

public interface ISessionManager
{
    /// <summary>
    /// Issues initial refresh token family for authenticated user session.
    /// </summary>
    Task<string> CreateSessionAsync(Guid userId, Guid orgId, string? deviceFingerprint, string? ipAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotates refresh token. Detects token reuse (TS-08) and revokes family on breach.
    /// </summary>
    Task<RefreshSessionResult> RotateSessionAsync(string rawRefreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes active session family upon logout.
    /// </summary>
    Task RevokeSessionFamilyAsync(Guid tokenFamilyId, CancellationToken cancellationToken = default);
}
