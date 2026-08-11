namespace QuotesApi.Models.Auth;

public class RefreshToken
{
    public int Id { get; set; }

    public string Token { get; set; } = "";

    public int UserId { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public string? ReplacedByToken { get; set; }
}
