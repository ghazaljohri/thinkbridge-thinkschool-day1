using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QuotesApi.Data;
using QuotesApi.Models.Auth;
using QuotesApi.Options;
using QuotesApi.Services;
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
            RefreshTokenService refreshTokenService,
            IOptionsSnapshot<JwtOptions> jwtOptions,
            CancellationToken cancellationToken) =>
        {
            var user = await db.Users
                .SingleOrDefaultAsync(
                    u => u.Email == request.Email,
                    cancellationToken);

            if (user is null)
                return Results.Unauthorized();

            bool passwordValid;
            using (var activity = QuotesApiActivitySource.Source.StartActivity("verify-password"))
            {
                activity?.SetTag("user.id", user.Id);
                passwordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
            }

            if (!passwordValid)
                return Results.Unauthorized();

            var accessToken = tokenService.CreateAccessToken(user);
            var refreshToken = refreshTokenService.GenerateToken();

            db.RefreshTokens.Add(new RefreshToken
            {
                Token = refreshToken,
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.Add(jwtOptions.Value.RefreshTokenLifetime)
            });

            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(new
            {
                access_token = accessToken,
                refresh_token = refreshToken,
                expires_in = (int)jwtOptions.Value.AccessTokenLifetime.TotalSeconds
            });
        });

        app.MapPost("/api/auth/refresh", async (
            RefreshRequest request,
            AppDbContext db,
            JwtTokenService tokenService,
            RefreshTokenService refreshTokenService,
            IOptionsSnapshot<JwtOptions> jwtOptions,
            CancellationToken cancellationToken) =>
        {
            var storedToken = await db.RefreshTokens
                .SingleOrDefaultAsync(
                    t => t.Token == request.RefreshToken,
                    cancellationToken);

            if (storedToken is null ||
                storedToken.RevokedAt is not null ||
                storedToken.ExpiresAt <= DateTime.UtcNow)
            {
                return Results.Unauthorized();
            }

            // RefreshTokens.UserId has a cascade-delete FK to Users, so a stored token
            // that is unexpired and unrevoked is guaranteed to reference an existing user.
            var user = await db.Users
                .SingleAsync(
                    u => u.Id == storedToken.UserId,
                    cancellationToken);

            var newAccessToken = tokenService.CreateAccessToken(user);
            var newRefreshToken = refreshTokenService.GenerateToken();

            storedToken.RevokedAt = DateTime.UtcNow;
            storedToken.ReplacedByToken = newRefreshToken;

            db.RefreshTokens.Add(new RefreshToken
            {
                Token = newRefreshToken,
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.Add(jwtOptions.Value.RefreshTokenLifetime)
            });

            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(new
            {
                access_token = newAccessToken,
                refresh_token = newRefreshToken,
                expires_in = (int)jwtOptions.Value.AccessTokenLifetime.TotalSeconds
            });
        });
    }

    public sealed record LoginRequest(
        string Email,
        string Password);

    public sealed record RefreshRequest(
        string RefreshToken);
}
