using System;

namespace SlateIQ.Application.Abstractions.Security;

public sealed record AccessTokenClaims(
    Guid UserId,
    Guid OrgId,
    string Email,
    string Role,
    string Scope = "ORG");

public interface ITokenService
{
    /// <summary>
    /// Generates short-lived HMAC-SHA256 JWT access token (15 minutes).
    /// </summary>
    string GenerateAccessToken(AccessTokenClaims claims, TimeSpan? expiry = null);

    /// <summary>
    /// Validates signature and expiry of access token, returning extracted claims.
    /// </summary>
    AccessTokenClaims? ValidateAccessToken(string token);

    /// <summary>
    /// Generates temporary short-lived MFA challenge token (5 minutes).
    /// </summary>
    string GenerateMfaChallengeToken(Guid userId, Guid orgId, string email);

    /// <summary>
    /// Validates temporary MFA challenge token.
    /// </summary>
    (Guid UserId, Guid OrgId, string Email)? ValidateMfaChallengeToken(string token);
}
