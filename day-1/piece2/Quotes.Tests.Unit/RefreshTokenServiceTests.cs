using System.Security.Cryptography;
using FluentAssertions;
using QuotesApi.Services.Auth;

namespace Quotes.Tests.Unit;

public sealed class RefreshTokenServiceTests
{
    [Fact]
    public void GenerateToken_NewToken_ReturnsBase64Encoded64RandomBytes()
    {
        // Arrange
        var service = new RefreshTokenService();

        // Act
        var token = service.GenerateToken();

        // Assert
        var bytes = Convert.FromBase64String(token);
        bytes.Should().HaveCount(64);
    }

    [Fact]
    public void GenerateToken_CalledTwice_ReturnsDistinctTokens()
    {
        // Arrange
        var service = new RefreshTokenService();

        // Act
        var first = service.GenerateToken();
        var second = service.GenerateToken();

        // Assert
        first.Should().NotBe(second);
    }
}
