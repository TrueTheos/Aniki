using Aniki.Models;
using Aniki.Models.Anilist;
using Aniki.Services.Interfaces;

namespace Aniki.Services.Auth.Providers;

public class AnilistLoginProvider : ILoginProvider
{
    public string Name => "AniList";
    public string Id => "anilist";
    
    // TODO: Replace with a real Client ID from AniList API settings
    private const string ClientId = "15243";

    public string LoginUrl => $"https://anilist.co/api/v2/oauth/authorize?client_id={ClientId}&response_type=token";

    private readonly ITokenService _tokenService;
    private readonly IAnilistService _anilistService;
    
    public AnilistLoginProvider(ITokenService tokenService, IAnilistService anilistService)
    {
        _tokenService = tokenService;
        _anilistService = anilistService;
    }

    public Task<string?> LoginAsync(IProgress<string> progressReporter)
    {
        progressReporter.Report("Please open the login URL, authorize the app, and paste the token back into the application.");
        return Task.FromResult<string?>(null);
    }
    
    public async Task SaveTokenAsync(string token)
    {
        var tokenResponse = new TokenResponse
        {
            access_token = token,
            expires_in = 31536000 // Anilist tokens from this flow are long-lived (1 year)
        };
        await _tokenService.SaveTokensAsync(Id, tokenResponse);
    }

    public async Task<string?> CheckExistingLoginAsync()
    {
        StoredTokenData? tokenData = await _tokenService.LoadTokensAsync(Id);
        
        if (tokenData == null || string.IsNullOrEmpty(tokenData.AccessToken)) return null;

        try
        {
            _anilistService.SetToken(tokenData.AccessToken);
            Anilist_ViewerData? viewerData = await _anilistService.GetViewerAsync();
            return viewerData?.Name;
        }
        catch (Exception)
        {
            _tokenService.ClearTokens(Id);
            return null;
        }
    }

    public void Logout()
    {
        _tokenService.ClearTokens(Id);
        _anilistService.SetToken(string.Empty);
    }
}