using System.Diagnostics;
using Aniki.Models;
using Aniki.Models.Anilist;
using Aniki.Services.Anime;
using Aniki.Services.Interfaces;

namespace Aniki.Services.Auth.Providers;

public class AnilistLoginProvider : ILoginProvider
{
    public string Name => "AniList";
    public string Id => "AniList";
    
    private const string ClientId = "32652";

    public string LoginUrl => $"https://anilist.co/api/v2/oauth/authorize?client_id={ClientId}&response_type=token";

    private readonly ITokenService _tokenService;
    private readonly IAnimeService _animeService;
    
    public AnilistLoginProvider(ITokenService tokenService, IAnimeService animeService, AnilistService anilistService)
    {
        _tokenService = tokenService;
        _animeService = animeService;
        
        _animeService.RegisterProvider(anilistService.ProviderName, anilistService);
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
        var tokenResponse = new TokenResponse
        {
            access_token = token,
            expires_in = 31536000
        };
        await _tokenService.SaveTokensAsync(Id, tokenResponse);
    }

    public async Task<string?> CheckExistingLoginAsync()
    {
        StoredTokenData? tokenData = await _tokenService.LoadTokensAsync(Id);
        
        if (tokenData == null || string.IsNullOrEmpty(tokenData.AccessToken)) return null;

        try
        {
            _animeService.SetActiveProvider(Name, tokenData.AccessToken);
            UserData? userData = await _animeService.GetUserDataAsync();
            return userData?.Name;
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
        //todo _animeService.SetToken(string.Empty);
    }
    
    private void OpenBrowser(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}