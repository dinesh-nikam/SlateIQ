using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Npgsql;
using SlateIQ.Application.Abstractions.Audit;
using SlateIQ.Application.Abstractions.Security;

namespace SlateIQ.Api.Endpoints;

public sealed record LoginRequest(
    string Email,
    string Password,
    string TenantCode,
    string? DeviceFingerprint = null);

public sealed record LoginResponse(
    bool MfaRequired,
    string? MfaChallengeToken,
    string? AccessToken,
    string? RefreshToken,
    int? ExpiresIn,
    UserDto? User);

public sealed record UserDto(
    Guid Id,
    Guid OrgId,
    string Email,
    string FullName,
    string Role);

public sealed record MfaChallengeRequest(
    string MfaChallengeToken,
    string TotpCode,
    string? DeviceFingerprint = null);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record RefreshTokenResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn);

public sealed record LogoutRequest(string RefreshToken);

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/auth");

        group.MapPost("/login", HandleLoginAsync);
        group.MapPost("/mfa/challenge", HandleMfaChallengeAsync);
        group.MapPost("/refresh", HandleRefreshTokenAsync);
        group.MapPost("/logout", HandleLogoutAsync);

        return endpoints;
    }

    private static async Task<IResult> HandleLoginAsync(
        LoginRequest request,
        NpgsqlDataSource dataSource,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        ISessionManager sessionManager,
        IAuditWriter auditWriter,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.TenantCode))
        {
            return Results.BadRequest(new { error = "ERR-001", message = "Email, password, and tenant code are required." });
        }

        await using var connection = await dataSource.OpenConnectionAsync(ct);

        // 1. Verify tenant organization status
        const string orgSql = "SELECT id, status FROM tenancy.organization WHERE code = @code;";
        Guid orgId;
        string orgStatus;
        await using (var cmd = new NpgsqlCommand(orgSql, connection))
        {
            cmd.Parameters.AddWithValue("code", request.TenantCode.Trim().ToLowerInvariant());
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                return Results.NotFound(new { error = "ERR-005", message = "Organization not found." });
            }
            orgId = reader.GetGuid(0);
            orgStatus = reader.GetString(1);
        }

        if (orgStatus != "ACTIVE")
        {
            return Results.StatusCode(403, new { error = "ERR-006", message = $"Organization access denied: status is '{orgStatus}'." });
        }

        // Set session context for tenant RLS
        await using (var ctxCmd = new NpgsqlCommand("SELECT set_config('app.current_org_id', @orgId, true);", connection))
        {
            ctxCmd.Parameters.AddWithValue("orgId", orgId.ToString());
            await ctxCmd.ExecuteNonQueryAsync(ct);
        }

        // 2. Lookup User, Credentials, and Primary Membership Role
        const string userSql = @"
            SELECT 
                u.id, u.email, u.full_name, u.status, u.mfa_enforced, 
                u.failed_login_attempts, u.lockout_until,
                c.password_hash, c.salt,
                COALESCE(m.primary_role_code, 'VIEWER') as role_code
            FROM identity.user u
            JOIN identity.credential c ON u.id = c.user_id
            LEFT JOIN tenancy.membership m ON u.id = m.user_id AND m.org_id = u.home_org_id
            WHERE u.email = @email AND u.home_org_id = @orgId;";

        Guid userId;
        string email;
        string fullName;
        string userStatus;
        bool mfaEnforced;
        int failedAttempts;
        DateTimeOffset? lockoutUntil;
        string storedHash;
        string storedSalt;
        string roleCode;

        await using (var cmd = new NpgsqlCommand(userSql, connection))
        {
            cmd.Parameters.AddWithValue("email", request.Email.Trim().ToLowerInvariant());
            cmd.Parameters.AddWithValue("orgId", orgId);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                return Results.Unauthorized();
            }

            userId = reader.GetGuid(0);
            email = reader.GetString(1);
            fullName = reader.GetString(2);
            userStatus = reader.GetString(3);
            mfaEnforced = reader.GetBoolean(4);
            failedAttempts = reader.GetInt32(5);
            lockoutUntil = reader.IsDBNull(6) ? null : reader.GetDateTime(6);
            storedHash = reader.GetString(7);
            storedSalt = reader.GetString(8);
            roleCode = reader.GetString(9);
        }

        if (userStatus != "ACTIVE")
        {
            return Results.StatusCode(403, new { error = "ERR-007", message = $"User account is {userStatus}." });
        }

        if (lockoutUntil.HasValue && lockoutUntil.Value > DateTimeOffset.UtcNow)
        {
            return Results.StatusCode(423, new { error = "ERR-003", message = $"Account locked due to consecutive failed attempts until {lockoutUntil:u}." });
        }

        // 3. Verify Argon2id Password Hash
        bool passwordValid = passwordHasher.VerifyPassword(request.Password, storedSalt, storedHash);
        if (!passwordValid)
        {
            failedAttempts++;
            var newLockout = failedAttempts >= 5 ? DateTimeOffset.UtcNow.AddMinutes(15) : (DateTimeOffset?)null;

            const string updateFailedSql = @"
                UPDATE identity.user 
                SET failed_login_attempts = @attempts, lockout_until = @lockout, updated_at = now()
                WHERE id = @id;";

            await using (var updateCmd = new NpgsqlCommand(updateFailedSql, connection))
            {
                updateCmd.Parameters.AddWithValue("attempts", failedAttempts);
                updateCmd.Parameters.AddWithValue("lockout", (object?)newLockout ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("id", userId);
                await updateCmd.ExecuteNonQueryAsync(ct);
            }

            return Results.Unauthorized();
        }

        // Reset failed attempts on valid password
        if (failedAttempts > 0)
        {
            const string resetAttemptsSql = "UPDATE identity.user SET failed_login_attempts = 0, lockout_until = NULL, last_login_at = now() WHERE id = @id;";
            await using var resetCmd = new NpgsqlCommand(resetAttemptsSql, connection);
            resetCmd.Parameters.AddWithValue("id", userId);
            await resetCmd.ExecuteNonQueryAsync(ct);
        }

        // 4. Check MFA Requirement
        bool hasMfaFactor = false;
        const string mfaCheckSql = "SELECT COUNT(1) FROM identity.mfa_factor WHERE user_id = @userId AND factor_type = 'TOTP' AND is_verified = true;";
        await using (var mfaCmd = new NpgsqlCommand(mfaCheckSql, connection))
        {
            mfaCmd.Parameters.AddWithValue("userId", userId);
            hasMfaFactor = Convert.ToInt64(await mfaCmd.ExecuteScalarAsync(ct)) > 0;
        }

        if (mfaEnforced || hasMfaFactor)
        {
            var mfaChallengeToken = tokenService.GenerateMfaChallengeToken(userId, orgId, email);
            return Results.Ok(new LoginResponse(
                MfaRequired: true,
                MfaChallengeToken: mfaChallengeToken,
                AccessToken: null,
                RefreshToken: null,
                ExpiresIn: null,
                User: null));
        }

        // 5. Complete Login (No MFA required)
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        var refreshToken = await sessionManager.CreateSessionAsync(userId, orgId, request.DeviceFingerprint, ipAddress, ct);
        var accessToken = tokenService.GenerateAccessToken(new AccessTokenClaims(userId, orgId, email, roleCode));

        await auditWriter.WriteEventAsync(new AuditEventRecord(
            OrgId: orgId,
            ActorUserId: userId,
            Action: "user.login_succeeded",
            EntityType: "user",
            EntityId: userId,
            Metadata: JsonSerializer.Serialize(new { email, role = roleCode, ip = ipAddress })), ct);

        return Results.Ok(new LoginResponse(
            MfaRequired: false,
            MfaChallengeToken: null,
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpiresIn: 900,
            User: new UserDto(userId, orgId, email, fullName, roleCode)));
    }

    private static async Task<IResult> HandleMfaChallengeAsync(
        MfaChallengeRequest request,
        NpgsqlDataSource dataSource,
        ITokenService tokenService,
        ITotpService totpService,
        ISessionManager sessionManager,
        IAuditWriter auditWriter,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var claims = tokenService.ValidateMfaChallengeToken(request.MfaChallengeToken);
        if (claims == null)
        {
            return Results.Unauthorized();
        }

        var (userId, orgId, email) = claims.Value;
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        // Fetch TOTP secret
        const string secretSql = "SELECT secret_encrypted FROM identity.mfa_factor WHERE user_id = @userId AND factor_type = 'TOTP' AND is_verified = true;";
        string? secret;
        await using (var cmd = new NpgsqlCommand(secretSql, connection))
        {
            cmd.Parameters.AddWithValue("userId", userId);
            secret = (string?)await cmd.ExecuteScalarAsync(ct);
        }

        if (string.IsNullOrWhiteSpace(secret))
        {
            return Results.BadRequest(new { error = "ERR-004", message = "No verified MFA factor enrolled." });
        }

        bool isValid = totpService.ValidateCode(secret, request.TotpCode);
        if (!isValid)
        {
            return Results.Unauthorized();
        }

        // Fetch primary role and full name
        const string userSql = @"
            SELECT u.full_name, COALESCE(m.primary_role_code, 'VIEWER')
            FROM identity.user u
            LEFT JOIN tenancy.membership m ON u.id = m.user_id AND m.org_id = u.home_org_id
            WHERE u.id = @userId;";

        string fullName = string.Empty;
        string roleCode = "VIEWER";
        await using (var cmd = new NpgsqlCommand(userSql, connection))
        {
            cmd.Parameters.AddWithValue("userId", userId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                fullName = reader.GetString(0);
                roleCode = reader.GetString(1);
            }
        }

        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        var refreshToken = await sessionManager.CreateSessionAsync(userId, orgId, request.DeviceFingerprint, ipAddress, ct);
        var accessToken = tokenService.GenerateAccessToken(new AccessTokenClaims(userId, orgId, email, roleCode));

        await auditWriter.WriteEventAsync(new AuditEventRecord(
            OrgId: orgId,
            ActorUserId: userId,
            Action: "user.mfa_verified",
            EntityType: "user",
            EntityId: userId,
            Metadata: JsonSerializer.Serialize(new { email, role = roleCode })), ct);

        return Results.Ok(new LoginResponse(
            MfaRequired: false,
            MfaChallengeToken: null,
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpiresIn: 900,
            User: new UserDto(userId, orgId, email, fullName, roleCode)));
    }

    private static async Task<IResult> HandleRefreshTokenAsync(
        RefreshTokenRequest request,
        NpgsqlDataSource dataSource,
        ISessionManager sessionManager,
        ITokenService tokenService,
        CancellationToken ct)
    {
        var rotationResult = await sessionManager.RotateSessionAsync(request.RefreshToken, ct);
        if (!rotationResult.IsSuccess)
        {
            if (rotationResult.ErrorCode == "TOKEN_REUSE_DETECTED")
            {
                return Results.StatusCode(401, new { error = "ERR-011", message = "Security breach: Refresh token family revoked due to reuse detection (TS-08)." });
            }
            return Results.Unauthorized();
        }

        // Fetch user email and role for rotated token
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        const string userSql = @"
            SELECT u.email, COALESCE(m.primary_role_code, 'VIEWER')
            FROM identity.user u
            LEFT JOIN tenancy.membership m ON u.id = m.user_id AND m.org_id = @orgId
            WHERE u.id = @userId;";

        string email = string.Empty;
        string roleCode = "VIEWER";
        await using (var cmd = new NpgsqlCommand(userSql, connection))
        {
            cmd.Parameters.AddWithValue("userId", rotationResult.UserId!.Value);
            cmd.Parameters.AddWithValue("orgId", rotationResult.OrgId!.Value);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                email = reader.GetString(0);
                roleCode = reader.GetString(1);
            }
        }

        var newAccessToken = tokenService.GenerateAccessToken(
            new AccessTokenClaims(rotationResult.UserId!.Value, rotationResult.OrgId!.Value, email, roleCode));

        return Results.Ok(new RefreshTokenResponse(
            AccessToken: newAccessToken,
            RefreshToken: rotationResult.NewRefreshToken!,
            ExpiresIn: 900));
    }

    private static async Task<IResult> HandleLogoutAsync(
        LogoutRequest request,
        ISessionManager sessionManager,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.RefreshToken) && request.RefreshToken.Contains('.'))
        {
            var parts = request.RefreshToken.Split('.', 2);
            if (Guid.TryParse(parts[0], out var familyId))
            {
                await sessionManager.RevokeSessionFamilyAsync(familyId, ct);
            }
        }

        return Results.NoContent();
    }
}
