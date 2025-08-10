namespace Aniki.Models;

public class TokenResponse
{
    public string? access_token { get; set; }
    public string? refresh_token { get; set; }
    public int expires_in { get; set; }
    public string? token_type { get; set; }
}

public class StoredTokenData
{
    public required string AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}