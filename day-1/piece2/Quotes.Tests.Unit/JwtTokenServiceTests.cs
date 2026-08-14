using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using QuotesApi.Models.Auth;
using QuotesApi.Options;
using QuotesApi.Services.Auth;

namespace Quotes.Tests.Unit;

public sealed class JwtTokenServiceTests
{
    [Fact]
    public void CreateAccessToken_ConfiguredUser_IncludesExpectedClaims()
    {
        // Arrange
        var jwtOptions = Substitute.For<IOptionsSnapshot<JwtOptions>>();
        jwtOptions.Value.Returns(new JwtOptions
        {
            SigningKey = "a-development-key-that-is-long-enough-for-hmac-sha256"
        });
        var service = new JwtTokenService(jwtOptions);
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
    public void CreateAccessToken_AccessTokenLifetimeConfigured_SetsMatchingExpiry()
    {
        // Arrange
        var jwtOptions = Substitute.For<IOptionsSnapshot<JwtOptions>>();
        jwtOptions.Value.Returns(new JwtOptions
        {
            SigningKey = "a-development-key-that-is-long-enough-for-hmac-sha256",
            AccessTokenLifetime = TimeSpan.FromMinutes(5)
        });
        var service = new JwtTokenService(jwtOptions);

        // Act
        var token = service.CreateAccessToken(new User { Id = 1, Email = "test@example.com" });
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        // Assert
        jwt.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(5), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void CreateAccessToken_SigningKeyMissing_ThrowsInvalidOperationException()
    {
        // Arrange
        var jwtOptions = Substitute.For<IOptionsSnapshot<JwtOptions>>();
        jwtOptions.Value.Returns(new JwtOptions { SigningKey = "" });
        var service = new JwtTokenService(jwtOptions);

        // Act
        var act = () => service.CreateAccessToken(new User());

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("JWT signing key is not configured.");
    }
}
