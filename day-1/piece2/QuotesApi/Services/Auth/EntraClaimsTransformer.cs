using System.Security.Claims;

namespace QuotesApi.Services.Auth;

public static class EntraClaimsTransformer
{
    public static void ApplyScopeClaims(ClaimsPrincipal? principal)
    {
        var scope = principal?.FindFirst("scp")?.Value;

        if (string.IsNullOrWhiteSpace(scope) || principal?.Identity is not ClaimsIdentity identity)
            return;

        foreach (var value in scope.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            identity.AddClaim(new Claim("scope", value));
    }
}
