using System.ComponentModel.DataAnnotations;

namespace QuotesApi.Options;

public sealed record EntraOptions
{
    [Required]
    public required string TenantId { get; init; }

    [Required]
    public required string ClientId { get; init; }

    public string? Audience { get; init; }

    public string Authority => $"https://login.microsoftonline.com/{TenantId}/v2.0";

    public string EffectiveAudience => Audience ?? $"api://{ClientId}";
}
