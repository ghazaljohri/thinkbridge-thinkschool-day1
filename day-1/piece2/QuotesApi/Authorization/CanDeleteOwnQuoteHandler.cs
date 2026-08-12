using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using QuotesApi.Models;

namespace QuotesApi.Authorization;

public sealed class CanDeleteOwnQuoteHandler
    : AuthorizationHandler<CanDeleteOwnQuoteRequirement, Quote>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CanDeleteOwnQuoteRequirement requirement,
        Quote quote)
    {
        var email = context.User.Claims
            .FirstOrDefault(c =>
                c.Type == "email" ||
                c.Type == ClaimTypes.Email)
            ?.Value;

        if (!string.IsNullOrWhiteSpace(email) &&
            string.Equals(
                email.Trim(),
                quote.Author.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
