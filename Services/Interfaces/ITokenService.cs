namespace Aniki.Services.Interfaces;

public interface ITokenService
{
    void Init();
    Task<StoredTokenData?> LoadTokensAsync(string providerId);
    Task SaveTokensAsync(string providerId, TokenResponse tokenResponse);
    void ClearTokens(string providerId);
    string GetAccessToken(string providerId);
    bool HasValidToken(string providerId);
}