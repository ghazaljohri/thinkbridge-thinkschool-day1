using System.IdentityModel.Tokens.Jwt;

namespace QuotesApi.Services.Auth;

public static class AuthSchemeSelector
{
    public static string SelectScheme(
        string? authorizationHeaderValue,
        string entraAuthority,
        string localJwtScheme,
        string entraJwtScheme)
    {
        var token = authorizationHeaderValue?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
            ? authorizationHeaderValue["Bearer ".Length..].Trim()
            : null;

        if (string.IsNullOrWhiteSpace(token))
            return localJwtScheme;

        try
        {
            var issuer = new JwtSecurityTokenHandler().ReadJwtToken(token).Issuer;

            if (string.Equals(issuer, entraAuthority, StringComparison.OrdinalIgnoreCase))
                return entraJwtScheme;
        }
        catch (ArgumentException)
        {
            // Malformed/non-JWT bearer tokens fall through to the local handler,
            // which returns the standard authentication failure response.
        }

        return localJwtScheme;
    }
}
