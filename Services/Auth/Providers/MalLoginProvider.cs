using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Aniki.Models;
using Aniki.Models.MAL;
using Aniki.Services.Anime;
using Aniki.Services.Interfaces;

namespace Aniki.Services.Auth.Providers;

public class MalLoginProvider : ILoginProvider
{
    private const string ClientId = "dc4a7501af14aec92b98f719b666c37c";
    private const string RedirectUri = "http://localhost:8000/callback";
    private string _codeVerifier = "";
    private readonly HttpListener _httpListener = new();

    private readonly ITokenService _tokenService;
    private readonly IAnimeService _animeService;

    public ILoginProvider.ProviderType Provider => ILoginProvider.ProviderType.MAL;

    public string LoginUrl => $"https://myanimelist.net/v1/oauth2/authorize" +
                              $"?response_type=code" +
                              $"&client_id={ClientId}" +
                              $"&code_challenge={_codeVerifier}" +
                              $"&code_challenge_method=plain" +
                              $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                              $"&state={Guid.NewGuid()}";
    
    public MalLoginProvider(ITokenService tokenService, IAnimeService animeService, MalService malService)
    {
        _tokenService = tokenService;
        _animeService = animeService;
        
        _animeService.RegisterProvider(malService.Provider, malService);
    }
    
    public async Task<string?> LoginAsync(IProgress<string> progressReporter)
    {
        try
        {
            _codeVerifier = "this_is_a_long_string_for_pkce_but_mal_uses_plain_so_it_doesnt_matter";

            _httpListener.Prefixes.Clear();
            _httpListener.Prefixes.Add(RedirectUri.EndsWith("/") ? RedirectUri : RedirectUri + "/");
            _httpListener.Start();

            progressReporter.Report("Redirecting to MyAnimeList...");

            OpenBrowser(LoginUrl);
            progressReporter.Report("Waiting for authentication in browser...");

            HttpListenerContext context = await _httpListener.GetContextAsync();
            string code = context.Request.QueryString["code"] ?? throw new InvalidOperationException("Failed to get code from query string.");

            string responseString = "<html><head><title>Auth Success</title></head><body><h1>Authentication successful!</h1><p>You can close this browser tab/window and return to Aniki.</p><script>window.close();</script></body></html>";
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            context.Response.ContentType = "text/html";
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            context.Response.Close();
            _httpListener.Stop();

            if (string.IsNullOrEmpty(code))
            {
                progressReporter.Report("Authentication failed or cancelled.");
                return null;
            }

            progressReporter.Report("Exchanging code for token...");
            bool success = await ExchangeCodeForTokenAsync(code);
            if (success)
            {
                return await CheckExistingLoginAsync(); 
            }
            
            progressReporter.Report("Authentication failed or cancelled.");
            return null;
        }
        catch (HttpListenerException hlex) when (hlex.ErrorCode == 5)
        {
            progressReporter.Report($"Error: Access denied starting listener on {RedirectUri}.\nTry running Aniki as Administrator OR use netsh http add urlacl url={RedirectUri}/ user=Everyone");
            return null;
        }
        catch (Exception ex)
        {
            progressReporter.Report($"Authentication error: {ex.Message}");
            if (_httpListener.IsListening) _httpListener.Stop();
            return null;
        }
        finally
        {
            _httpListener?.Close();
        }
    }
    
    public Task SaveTokenAsync(string token)
    {
        return Task.CompletedTask;
    }

    public async Task<string?> CheckExistingLoginAsync()
    {
        StoredTokenData? tokenData = await _tokenService.LoadTokensAsync(Provider);

        if (tokenData == null || string.IsNullOrEmpty(tokenData.AccessToken)) return null;
        
        try
        {
            _animeService.SetActiveProvider(Provider, tokenData.AccessToken);
            UserData? userData = await _animeService.GetUserDataAsync();
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
    
    private async Task<bool> ExchangeCodeForTokenAsync(string code)
    {
        try
        {
            using HttpClient client = new();
            FormUrlEncodedContent content = new(new[]
            {
                new KeyValuePair<string, string>("client_id", ClientId),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("code_verifier", _codeVerifier),
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("redirect_uri", RedirectUri)
            });

            HttpResponseMessage response = await client.PostAsync("https://myanimelist.net/v1/oauth2/token", content);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) return false;
            
            TokenResponse? tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (tokenResponse == null) return false;
            
            await _tokenService.SaveTokensAsync(Provider, tokenResponse);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void OpenBrowser(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}