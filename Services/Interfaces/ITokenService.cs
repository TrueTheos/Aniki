using Aniki.Services.Auth;

namespace Aniki.Services.Interfaces;

public interface ITokenService
{
    void Init();
    Task<StoredTokenData?> LoadTokensAsync(ILoginProvider.ProviderType providerId);
    Task SaveTokensAsync(ILoginProvider.ProviderType providerId, TokenResponse tokenResponse);
    void ClearTokens(ILoginProvider.ProviderType providerId);
    string GetAccessToken(ILoginProvider.ProviderType providerId);
    bool HasValidToken(ILoginProvider.ProviderType providerId);
}