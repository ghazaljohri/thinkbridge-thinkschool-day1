using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using QuotesApi.Authorization;
using QuotesApi.Models;

namespace Quotes.Tests.Unit;

public sealed class CanDeleteOwnQuoteHandlerTests
{
    [Fact]
    public async Task HandleAsync_EmailMatchesOwnerIgnoringCaseAndWhitespace_Succeeds()
    {
        // Arrange
        var requirement = new CanDeleteOwnQuoteRequirement();
        var handler = new CanDeleteOwnQuoteHandler();
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("email", "  TEST@example.com ")], "Bearer"));
        var quote = Quote.Create("test@example.COM", "A quote", DateTimeOffset.UtcNow);
        var context = new AuthorizationHandlerContext([requirement], user, quote);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_ClaimTypesEmailMatchesOwner_Succeeds()
    {
        // Arrange
        var requirement = new CanDeleteOwnQuoteRequirement();
        var handler = new CanDeleteOwnQuoteHandler();
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Email, "test@example.com")], "Bearer"));
        var quote = Quote.Create("test@example.com", "A quote", DateTimeOffset.UtcNow);
        var context = new AuthorizationHandlerContext([requirement], user, quote);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_EmailDoesNotMatchOwner_DoesNotSucceed()
    {
        // Arrange
        var requirement = new CanDeleteOwnQuoteRequirement();
        var handler = new CanDeleteOwnQuoteHandler();
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("email", "other@example.com")], "Bearer"));
        var quote = Quote.Create("test@example.com", "A quote", DateTimeOffset.UtcNow);
        var context = new AuthorizationHandlerContext([requirement], user, quote);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_EmailClaimMissing_DoesNotSucceed()
    {
        // Arrange
        var requirement = new CanDeleteOwnQuoteRequirement();
        var handler = new CanDeleteOwnQuoteHandler();
        var user = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "Bearer"));
        var quote = Quote.Create("test@example.com", "A quote", DateTimeOffset.UtcNow);
        var context = new AuthorizationHandlerContext([requirement], user, quote);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
    }
}
