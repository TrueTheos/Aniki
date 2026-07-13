using System.Diagnostics;
using Aniki.Services.Anime;
using Aniki.Services.Anime.Providers;
using Aniki.Services.Interfaces;

namespace Aniki.Services.Auth.Providers;

internal sealed class AnilistLoginProvider : ILoginProvider
{
    public ILoginProvider.ProviderType Provider => ILoginProvider.ProviderType.AniList;
    private const string CLIENT_ID = "32652";

    public string LoginUrl => $"https://anilist.co/api/v2/oauth/authorize?client_id={CLIENT_ID}&response_type=token";

    private readonly ITokenService _tokenService;
    private readonly IAnimeService _animeService;
    
    public AnilistLoginProvider(ITokenService tokenService, IAnimeService animeService, AnilistService anilistService)
    {
        _tokenService = tokenService;
        _animeService = animeService;
        
        _animeService.RegisterProvider(anilistService.Provider, anilistService);
    }

    public Task<string?> LoginAsync(IProgress<string> progressReporter)
    {
        progressReporter.Report("Redirecting to Anilist for authentication...");
        OpenBrowser(LoginUrl);
        progressReporter.Report("Please authorize the app and paste the token back into the application.");
        return Task.FromResult<string?>(null);
    }
    
    public async Task SaveTokenAsync(string token)
    {
        TokenResponse tokenResponse = new()
        {
            AccessToken = token,
            ExpiresIn = 31536000
        };
        await _tokenService.SaveTokensAsync(Provider, tokenResponse).ConfigureAwait(true);
    }

    public async Task<string?> CheckExistingLoginAsync()
    {
        StoredTokenData? tokenData = await _tokenService.LoadTokensAsync(Provider).ConfigureAwait(true);
        
        if (tokenData == null || string.IsNullOrEmpty(tokenData.AccessToken)) return null;

        try
        {
            await _animeService.SetActiveProviderAsync(Provider, tokenData.AccessToken).ConfigureAwait(true);
            UserData? userData = await _animeService.GetUserDataAsync().ConfigureAwait(true);
            return userData?.Name;
        }
        catch (Exception)
        {
            _tokenService.ClearTokens(Provider);
            return null;
        }
    }

    public void Logout()
    {
        _tokenService.ClearTokens(Provider); 
    }
    
    private static void OpenBrowser(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}