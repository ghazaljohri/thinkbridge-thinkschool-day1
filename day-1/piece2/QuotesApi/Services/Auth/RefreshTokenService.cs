using System.Security.Cryptography;

namespace QuotesApi.Services.Auth;

public sealed class RefreshTokenService
{
    public string GenerateToken()
    {
        return Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(64));
    }
}
