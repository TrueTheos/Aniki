namespace Aniki.Services.Interfaces;

public interface ITokenService
{
    public void Init();
    public Task<StoredTokenData?> LoadTokensAsync();
    public Task SaveTokensAsync(TokenResponse tokenResponse);
    public void ClearTokens();
    public string GetAccessToken();
    public bool HasValidToken();
}