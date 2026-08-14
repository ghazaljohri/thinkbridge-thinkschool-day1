using System.Security.Claims;
using FluentAssertions;
using QuotesApi.Services.Auth;

namespace Quotes.Tests.Unit;

public sealed class EntraClaimsTransformerTests
{
    [Fact]
    public void ApplyScopeClaims_ScpClaimWithMultipleValues_AddsOneScopeClaimPerValue()
    {
        // Arrange
        var identity = new ClaimsIdentity([new Claim("scp", "quotes.write quotes.read")]);
        var principal = new ClaimsPrincipal(identity);

        // Act
        EntraClaimsTransformer.ApplyScopeClaims(principal);

        // Assert
        identity.Claims.Where(c => c.Type == "scope").Select(c => c.Value)
            .Should().BeEquivalentTo(["quotes.write", "quotes.read"]);
    }

    [Fact]
    public void ApplyScopeClaims_NoScpClaim_AddsNoScopeClaims()
    {
        // Arrange
        var identity = new ClaimsIdentity();
        var principal = new ClaimsPrincipal(identity);

        // Act
        EntraClaimsTransformer.ApplyScopeClaims(principal);

        // Assert
        identity.Claims.Should().NotContain(c => c.Type == "scope");
    }

    [Fact]
    public void ApplyScopeClaims_WhitespaceScpClaim_AddsNoScopeClaims()
    {
        // Arrange
        var identity = new ClaimsIdentity([new Claim("scp", "   ")]);
        var principal = new ClaimsPrincipal(identity);

        // Act
        EntraClaimsTransformer.ApplyScopeClaims(principal);

        // Assert
        identity.Claims.Should().NotContain(c => c.Type == "scope");
    }

    [Fact]
    public void ApplyScopeClaims_NullPrincipal_DoesNotThrow()
    {
        // Act
        var act = () => EntraClaimsTransformer.ApplyScopeClaims(null);

        // Assert
        act.Should().NotThrow();
    }
}
