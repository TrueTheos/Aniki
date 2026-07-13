using System.Text.Json.Serialization;

namespace Aniki.Models;

internal sealed class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }
    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }
}

internal sealed class StoredTokenData
{
    public required string AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}