using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SlateIQ.Application.Abstractions.Security;

namespace SlateIQ.Infrastructure.Security;

public sealed class JwtTokenService : ITokenService
{
    private readonly byte[] _signingKey;
    private const string Issuer = "SlateIQ";
    private const string Audience = "SlateIQ.Api";

    public JwtTokenService(IConfiguration? configuration = null)
    {
        var secret = configuration?["Jwt:SecretKey"] 
            ?? "SlateIQ_ProductionCostIntelligence_SecretKey_2026_Min256BitsLongKey!";
        _signingKey = Encoding.UTF8.GetBytes(secret);
    }

    public string GenerateAccessToken(AccessTokenClaims claims, TimeSpan? expiry = null)
    {
        var duration = expiry ?? TimeSpan.FromMinutes(15);
        var now = DateTimeOffset.UtcNow;
        var exp = now.Add(duration).ToUnixTimeSeconds();
        var iat = now.ToUnixTimeSeconds();

        var header = new { alg = "HS256", typ = "JWT" };
        var payload = new
        {
            sub = claims.UserId.ToString(),
            org_id = claims.OrgId.ToString(),
            email = claims.Email,
            role = claims.Role,
            scope = claims.Scope,
            iss = Issuer,
            aud = Audience,
            iat = iat,
            exp = exp,
            jti = Guid.NewGuid().ToString("N")
        };

        return CreateJwt(header, payload);
    }

    public AccessTokenClaims? ValidateAccessToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var parts = token.Split('.');
        if (parts.Length != 3) return null;

        var headerEncoded = parts[0];
        var payloadEncoded = parts[1];
        var signatureProvided = parts[2];

        // Verify HMAC-SHA256 signature
        var unsignedData = $"{headerEncoded}.{payloadEncoded}";
        var computedSignature = ComputeHmacSha256(unsignedData);
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(signatureProvided), 
            Encoding.UTF8.GetBytes(computedSignature)))
        {
            return null;
        }

        try
        {
            var payloadBytes = Base64UrlDecode(payloadEncoded);
            using var doc = JsonDocument.Parse(payloadBytes);
            var root = doc.RootElement;

            // Verify expiration
            if (!root.TryGetProperty("exp", out var expProp)) return null;
            var expUnix = expProp.GetInt64();
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= expUnix) return null;

            // Extract claims
            var userId = Guid.Parse(root.GetProperty("sub").GetString()!);
            var orgId = Guid.Parse(root.GetProperty("org_id").GetString()!);
            var email = root.GetProperty("email").GetString()!;
            var role = root.GetProperty("role").GetString()!;
            var scope = root.TryGetProperty("scope", out var scopeProp) ? scopeProp.GetString() ?? "ORG" : "ORG";

            return new AccessTokenClaims(userId, orgId, email, role, scope);
        }
        catch
        {
            return null;
        }
    }

    public string GenerateMfaChallengeToken(Guid userId, Guid orgId, string email)
    {
        var now = DateTimeOffset.UtcNow;
        var exp = now.AddMinutes(5).ToUnixTimeSeconds();

        var header = new { alg = "HS256", typ = "JWT" };
        var payload = new
        {
            sub = userId.ToString(),
            org_id = orgId.ToString(),
            email = email,
            mfa_pending = true,
            iss = Issuer,
            aud = Audience,
            exp = exp,
            jti = Guid.NewGuid().ToString("N")
        };

        return CreateJwt(header, payload);
    }

    public (Guid UserId, Guid OrgId, string Email)? ValidateMfaChallengeToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var parts = token.Split('.');
        if (parts.Length != 3) return null;

        var unsignedData = $"{parts[0]}.{parts[1]}";
        var computedSig = ComputeHmacSha256(unsignedData);
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(parts[2]), 
            Encoding.UTF8.GetBytes(computedSig)))
        {
            return null;
        }

        try
        {
            var payloadBytes = Base64UrlDecode(parts[1]);
            using var doc = JsonDocument.Parse(payloadBytes);
            var root = doc.RootElement;

            if (!root.TryGetProperty("mfa_pending", out var mfaPending) || !mfaPending.GetBoolean())
                return null;

            var expUnix = root.GetProperty("exp").GetInt64();
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= expUnix) return null;

            var userId = Guid.Parse(root.GetProperty("sub").GetString()!);
            var orgId = Guid.Parse(root.GetProperty("org_id").GetString()!);
            var email = root.GetProperty("email").GetString()!;

            return (userId, orgId, email);
        }
        catch
        {
            return null;
        }
    }

    private string CreateJwt(object header, object payload)
    {
        var headerJson = JsonSerializer.Serialize(header);
        var payloadJson = JsonSerializer.Serialize(payload);

        var headerEncoded = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
        var payloadEncoded = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));

        var unsignedData = $"{headerEncoded}.{payloadEncoded}";
        var signatureEncoded = ComputeHmacSha256(unsignedData);

        return $"{unsignedData}.{signatureEncoded}";
    }

    private string ComputeHmacSha256(string data)
    {
        using var hmac = new HMACSHA256(_signingKey);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    private static byte[] Base64UrlDecode(string input)
    {
        string incoming = input.Replace('-', '+').Replace('_', '/');
        switch (incoming.Length % 4)
        {
            case 2: incoming += "=="; break;
            case 3: incoming += "="; break;
        }
        return Convert.FromBase64String(incoming);
    }
}
