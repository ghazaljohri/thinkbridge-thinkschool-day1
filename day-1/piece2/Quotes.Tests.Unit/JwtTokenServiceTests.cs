using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using QuotesApi.Models.Auth;
using QuotesApi.Services.Auth;

namespace Quotes.Tests.Unit;

public sealed class JwtTokenServiceTests
{
    [Fact]
    public void CreateAccessToken_ConfiguredUser_IncludesExpectedClaims()
    {
        // Arrange
        var configuration = Substitute.For<IConfiguration>();
        configuration["Jwt:Key"].Returns("a-development-key-that-is-long-enough-for-hmac-sha256");
        var service = new JwtTokenService(configuration);
        var user = new User { Id = 12, Email = "test@example.com" };

        // Act
        var token = service.CreateAccessToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        // Assert
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == "12");
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "test@example.com");
        jwt.Claims.Should().Contain(c => c.Type == "scope" && c.Value == "quotes.write");
    }

    [Fact]
    public void CreateAccessToken_JwtKeyMissing_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = Substitute.For<IConfiguration>();
        configuration["Jwt:Key"].Returns((string?)null);
        var service = new JwtTokenService(configuration);

        // Act
        var act = () => service.CreateAccessToken(new User());

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("JWT key is not configured.");
    }
}
