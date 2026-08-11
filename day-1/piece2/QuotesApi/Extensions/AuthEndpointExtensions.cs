using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Services.Auth;

namespace QuotesApi.Extensions;

public static class AuthEndpointExtensions
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/login", async (
            LoginRequest request,
            AppDbContext db,
            JwtTokenService tokenService,
            CancellationToken cancellationToken) =>
        {
            var user = await db.Users
                .SingleOrDefaultAsync(
                    u => u.Email == request.Email,
                    cancellationToken);

            if (user is null ||
                !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return Results.Unauthorized();
            }

            var accessToken = tokenService.CreateAccessToken(user);

            return Results.Ok(new
            {
                access_token = accessToken,
                expires_in = 1800
            });
        });
    }

    public sealed record LoginRequest(
        string Email,
        string Password);
}
