using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using QuotesApi.Services.Auth;

namespace Quotes.Tests.Unit;

public sealed class AuthSchemeSelectorTests
{
    private const string LocalScheme = "LocalJwt";
    private const string EntraScheme = "EntraJwt";
    private const string EntraAuthority = "https://login.microsoftonline.com/tenant-id/v2.0";

    [Fact]
    public void SelectScheme_NoAuthorizationHeader_ReturnsLocalScheme()
    {
        // Act
        var scheme = AuthSchemeSelector.SelectScheme("", EntraAuthority, LocalScheme, EntraScheme);

        // Assert
        scheme.Should().Be(LocalScheme);
    }

    [Fact]
    public void SelectScheme_NonBearerAuthorizationHeader_ReturnsLocalScheme()
    {
        // Act
        var scheme = AuthSchemeSelector.SelectScheme("Basic dXNlcjpwYXNz", EntraAuthority, LocalScheme, EntraScheme);

        // Assert
        scheme.Should().Be(LocalScheme);
    }

    [Fact]
    public void SelectScheme_MalformedBearerToken_ReturnsLocalScheme()
    {
        // Act
        var scheme = AuthSchemeSelector.SelectScheme("Bearer not-a-jwt", EntraAuthority, LocalScheme, EntraScheme);

        // Assert
        scheme.Should().Be(LocalScheme);
    }

    [Fact]
    public void SelectScheme_TokenIssuedByEntraAuthority_ReturnsEntraScheme()
    {
        // Arrange
        var token = CreateTokenWithIssuer(EntraAuthority);

        // Act
        var scheme = AuthSchemeSelector.SelectScheme($"Bearer {token}", EntraAuthority, LocalScheme, EntraScheme);

        // Assert
        scheme.Should().Be(EntraScheme);
    }

    [Fact]
    public void SelectScheme_TokenIssuedByOtherIssuer_ReturnsLocalScheme()
    {
        // Arrange
        var token = CreateTokenWithIssuer("https://some-other-issuer.example.com");

        // Act
        var scheme = AuthSchemeSelector.SelectScheme($"Bearer {token}", EntraAuthority, LocalScheme, EntraScheme);

        // Assert
        scheme.Should().Be(LocalScheme);
    }

    private static string CreateTokenWithIssuer(string issuer)
    {
        var token = new JwtSecurityToken(issuer: issuer, claims: []);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
