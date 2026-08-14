using System.ComponentModel.DataAnnotations;

namespace QuotesApi.Options;

public sealed record JwtOptions
{
    [Required]
    public required string SigningKey { get; init; }

    public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromMinutes(30);

    public TimeSpan RefreshTokenLifetime { get; init; } = TimeSpan.FromDays(7);
}
