using System;
using OtpNet;
using SlateIQ.Application.Abstractions.Security;

namespace SlateIQ.Infrastructure.Security;

public sealed class TotpService : ITotpService
{
    public string GenerateSecretKey()
    {
        byte[] key = KeyGeneration.GenerateRandomKey(20); // 160 bits
        return Base32Encoding.ToString(key);
    }

    public bool VerifyCode(string secretKey, string candidateCode)
    {
        if (string.IsNullOrWhiteSpace(secretKey) || string.IsNullOrWhiteSpace(candidateCode))
            return false;

        try
        {
            byte[] keyBytes = Base32Encoding.ToBytes(secretKey);
            var totp = new Totp(keyBytes, step: 30, totpSize: 6);

            // Verify with window ±1 time step (30 seconds before/after)
            return totp.VerifyTotp(candidateCode.Trim(), out _, new VerificationWindow(previous: 1, future: 1));
        }
        catch
        {
            return false;
        }
    }
}
