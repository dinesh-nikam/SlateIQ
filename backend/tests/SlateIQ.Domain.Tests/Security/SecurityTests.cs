using FluentAssertions;
using SlateIQ.Infrastructure.Security;
using Xunit;

namespace SlateIQ.Domain.Tests.Security;

public class SecurityTests
{
    [Fact]
    public void Argon2id_HashAndVerify_PasswordMatches_Succeeds()
    {
        var hasher = new Argon2idPasswordHasher();
        string plainPassword = "SlateIQ@SuperSecure2026!";

        string hash = hasher.HashPassword(plainPassword);

        hash.Should().StartWith("$argon2id$v=19$m=65536,t=3,p=4$");

        bool isValid = hasher.VerifyPassword(plainPassword, hash);
        isValid.Should().BeTrue();
    }

    [Fact]
    public void Argon2id_WrongPassword_ReturnsFalse()
    {
        var hasher = new Argon2idPasswordHasher();
        string plainPassword = "CorrectPassword123";
        string wrongPassword = "WrongPassword456";

        string hash = hasher.HashPassword(plainPassword);

        bool isValid = hasher.VerifyPassword(wrongPassword, hash);
        isValid.Should().BeFalse();
    }

    [Fact]
    public void Totp_GenerateAndVerify_CurrentCode_Succeeds()
    {
        var totpService = new TotpService();
        string secretKey = totpService.GenerateSecretKey();

        secretKey.Should().NotBeNullOrWhiteSpace();

        // Generate current valid code using OtpNet
        var keyBytes = OtpNet.Base32Encoding.ToBytes(secretKey);
        var totp = new OtpNet.Totp(keyBytes, step: 30, totpSize: 6);
        string currentCode = totp.ComputeTotp();

        bool isValid = totpService.VerifyCode(secretKey, currentCode);
        isValid.Should().BeTrue();
    }

    [Fact]
    public void Totp_InvalidCode_ReturnsFalse()
    {
        var totpService = new TotpService();
        string secretKey = totpService.GenerateSecretKey();

        bool isValid = totpService.VerifyCode(secretKey, "000000");
        isValid.Should().BeFalse();
    }
}
