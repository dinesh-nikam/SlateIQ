using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using SlateIQ.Application.Abstractions.Security;

namespace SlateIQ.Infrastructure.Security;

public sealed class SessionManager : ISessionManager
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<SessionManager> _logger;
    private readonly TimeSpan _sessionTtl = TimeSpan.FromDays(30);

    public SessionManager(NpgsqlDataSource dataSource, ILogger<SessionManager> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
    }

    public async Task<string> CreateSessionAsync(Guid userId, Guid orgId, string? deviceFingerprint, string? ipAddress, CancellationToken cancellationToken = default)
    {
        Guid familyId = Guid.NewGuid();
        string rawToken = GenerateSecureToken();
        string tokenHash = ComputeTokenHash(rawToken);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        const string sql = @"
            INSERT INTO identity.refresh_session (
                id, user_id, org_id, token_family_id, token_hash,
                device_fingerprint, ip_address, expires_at, created_at
            ) VALUES (
                gen_random_uuid(), @userId, @orgId, @familyId, @tokenHash,
                @device, @ip::inet, @expiresAt, now()
            );";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("orgId", orgId);
        cmd.Parameters.AddWithValue("familyId", familyId);
        cmd.Parameters.AddWithValue("tokenHash", tokenHash);
        cmd.Parameters.AddWithValue("device", (object?)deviceFingerprint ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ip", (object?)ipAddress ?? DBNull.Value);
        cmd.Parameters.AddWithValue("expiresAt", DateTimeOffset.UtcNow.Add(_sessionTtl));

        await cmd.ExecuteNonQueryAsync(cancellationToken);

        return $"{familyId:N}.{rawToken}";
    }

    public async Task<RefreshSessionResult> RotateSessionAsync(string rawRefreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken) || !rawRefreshToken.Contains('.'))
            return new RefreshSessionResult(false, ErrorCode: "TOKEN_INVALID");

        var parts = rawRefreshToken.Split('.', 2);
        if (!Guid.TryParse(parts[0], out Guid tokenFamilyId))
            return new RefreshSessionResult(false, ErrorCode: "TOKEN_INVALID");

        string tokenSecret = parts[1];
        string tokenHash = ComputeTokenHash(tokenSecret);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // Fetch existing session record
        const string querySql = @"
            SELECT id, user_id, org_id, token_family_id, revoked_at, replaced_by_token_hash, expires_at
            FROM identity.refresh_session
            WHERE token_hash = @tokenHash
            FOR UPDATE;";

        Guid? userId = null;
        Guid? orgId = null;
        bool isRevoked = false;
        bool isReplaced = false;
        DateTimeOffset expiresAt = DateTimeOffset.MinValue;
        Guid sessionId = Guid.Empty;

        await using (var cmd = new NpgsqlCommand(querySql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("tokenHash", tokenHash);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                sessionId = reader.GetGuid(0);
                userId = reader.GetGuid(1);
                orgId = reader.GetGuid(2);
                isRevoked = !reader.IsDBNull(4);
                isReplaced = !reader.IsDBNull(5);
                expiresAt = reader.GetDateTime(6);
            }
            else
            {
                return new RefreshSessionResult(false, ErrorCode: "TOKEN_INVALID");
            }
        }

        // TS-08: Token Reuse Detection
        if (isReplaced || isRevoked)
        {
            _logger.LogWarning("SECURITY ALERT: Refresh token reuse detected for family {FamilyId}. Revoking entire family (TS-08).", tokenFamilyId);
            
            // Revoke all tokens in family
            const string revokeFamilySql = @"
                UPDATE identity.refresh_session
                SET revoked_at = now()
                WHERE token_family_id = @familyId AND revoked_at IS NULL;";

            await using var revokeCmd = new NpgsqlCommand(revokeFamilySql, connection, transaction);
            revokeCmd.Parameters.AddWithValue("familyId", tokenFamilyId);
            await revokeCmd.ExecuteNonQueryAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return new RefreshSessionResult(false, ErrorCode: "TOKEN_REVOKED_REUSE_DETECTED");
        }

        if (expiresAt <= DateTimeOffset.UtcNow)
        {
            return new RefreshSessionResult(false, ErrorCode: "TOKEN_EXPIRED");
        }

        // Issue rotated replacement token
        string newRawTokenSecret = GenerateSecureToken();
        string newTokenHash = ComputeTokenHash(newRawTokenSecret);

        const string updatePriorSql = @"
            UPDATE identity.refresh_session
            SET replaced_by_token_hash = @newTokenHash
            WHERE id = @sessionId;";

        await using (var updateCmd = new NpgsqlCommand(updatePriorSql, connection, transaction))
        {
            updateCmd.Parameters.AddWithValue("newTokenHash", newTokenHash);
            updateCmd.Parameters.AddWithValue("sessionId", sessionId);
            await updateCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        const string insertNewSql = @"
            INSERT INTO identity.refresh_session (
                id, user_id, org_id, token_family_id, token_hash,
                expires_at, created_at
            ) VALUES (
                gen_random_uuid(), @userId, @orgId, @familyId, @tokenHash,
                @expiresAt, now()
            );";

        await using (var insertCmd = new NpgsqlCommand(insertNewSql, connection, transaction))
        {
            insertCmd.Parameters.AddWithValue("userId", userId!.Value);
            insertCmd.Parameters.AddWithValue("orgId", orgId!.Value);
            insertCmd.Parameters.AddWithValue("familyId", tokenFamilyId);
            insertCmd.Parameters.AddWithValue("tokenHash", newTokenHash);
            insertCmd.Parameters.AddWithValue("expiresAt", DateTimeOffset.UtcNow.Add(_sessionTtl));
            await insertCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new RefreshSessionResult(
            IsSuccess: true,
            NewRefreshToken: $"{tokenFamilyId:N}.{newRawTokenSecret}",
            UserId: userId,
            OrgId: orgId
        );
    }

    public async Task RevokeSessionFamilyAsync(Guid tokenFamilyId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        const string sql = @"
            UPDATE identity.refresh_session
            SET revoked_at = now()
            WHERE token_family_id = @familyId AND revoked_at IS NULL;";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("familyId", tokenFamilyId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string GenerateSecureToken()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32); // 256 bits
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    private static string ComputeTokenHash(string rawToken)
    {
        using var sha256 = SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
