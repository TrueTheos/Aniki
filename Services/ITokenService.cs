

namespace Aniki.Services;

public interface ITokenService
{
    Task<StoredTokenData> LoadTokensAsync();
    Task SaveTokensAsync(TokenResponse tokenResponse);
    void ClearTokens();
    bool HasValidToken();
    string GetAccessToken();
}