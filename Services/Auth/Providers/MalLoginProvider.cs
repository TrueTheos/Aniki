using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Aniki.Services.Anime;
using Aniki.Services.Anime.Providers;
using Aniki.Services.Interfaces;

namespace Aniki.Services.Auth.Providers;

internal sealed class MalLoginProvider : ILoginProvider, IDisposable
{
    private static readonly HttpClient Client = new();
    private const string CLIENT_ID = "dc4a7501af14aec92b98f719b666c37c";
    private const string REDIRECT_URI = "http://localhost:8000/callback";
    private string _codeVerifier = "";
    private readonly HttpListener _httpListener = new();

    private readonly ITokenService _tokenService;
    private readonly IAnimeService _animeService;

    public ILoginProvider.ProviderType Provider => ILoginProvider.ProviderType.Mal;

    public string LoginUrl => $"https://myanimelist.net/v1/oauth2/authorize" +
                              $"?response_type=code" +
                              $"&client_id={CLIENT_ID}" +
                              $"&code_challenge={_codeVerifier}" +
                              $"&code_challenge_method=plain" +
                              $"&redirect_uri={Uri.EscapeDataString(REDIRECT_URI)}" +
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
            _httpListener.Prefixes.Add(REDIRECT_URI.EndsWith('/') ? REDIRECT_URI : REDIRECT_URI + "/");
            _httpListener.Start();

            progressReporter.Report("Redirecting to MyAnimeList...");

            OpenBrowser(LoginUrl);
            progressReporter.Report("Waiting for authentication in browser...");

            HttpListenerContext context = await _httpListener.GetContextAsync().ConfigureAwait(false);
            string code = context.Request.QueryString["code"] ?? throw new InvalidOperationException("Failed to get code from query string.");

            string responseString = "<html><head><title>Auth Success</title></head><body><h1>Authentication successful!</h1><p>You can close this browser tab/window and return to Aniki.</p><script>window.close();</script></body></html>";
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            context.Response.ContentType = "text/html";
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer).ConfigureAwait(false);
            context.Response.Close();
            _httpListener.Stop();

            if (string.IsNullOrEmpty(code))
            {
                progressReporter.Report("Authentication failed or cancelled.");
                return null;
            }

            progressReporter.Report("Exchanging code for token...");
            bool success = await ExchangeCodeForTokenAsync(code).ConfigureAwait(false);
            if (success)
            {
                return await CheckExistingLoginAsync().ConfigureAwait(false); 
            }
            
            progressReporter.Report("Authentication failed or cancelled.");
            return null;
        }
        catch (HttpListenerException hlex) when (hlex.ErrorCode == 5)
        {
            progressReporter.Report($"Error: Access denied starting listener on {REDIRECT_URI}.\nTry running Aniki as Administrator OR use netsh http add urlacl url={REDIRECT_URI}/ user=Everyone");
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
    
    public async Task SaveTokenAsync(string token)
    {
        TokenResponse tokenResponse = new()
        {
            AccessToken = token,
            ExpiresIn = 2415600
        };
        await _tokenService.SaveTokensAsync(Provider, tokenResponse).ConfigureAwait(false);
    }

    public async Task<string?> CheckExistingLoginAsync()
    {
        StoredTokenData? tokenData = await _tokenService.LoadTokensAsync(Provider).ConfigureAwait(false);

        if (tokenData == null || string.IsNullOrEmpty(tokenData.AccessToken)) return null;
        
        try
        {
            await _animeService.SetActiveProviderAsync(Provider, tokenData.AccessToken).ConfigureAwait(false);
            UserData? userData = await _animeService.GetUserDataAsync().ConfigureAwait(false);
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
            using FormUrlEncodedContent content = new([
                new KeyValuePair<string, string>("client_id", CLIENT_ID),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("code_verifier", _codeVerifier),
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("redirect_uri", REDIRECT_URI)
            ]);

            HttpResponseMessage response = await Client.PostAsync("https://myanimelist.net/v1/oauth2/token", content).ConfigureAwait(false);
            string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode) return false;
            
            TokenResponse? tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (tokenResponse == null) return false;
            
            await _tokenService.SaveTokensAsync(Provider, tokenResponse).ConfigureAwait(false);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void OpenBrowser(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    public void Dispose()
    {
        _httpListener.Close();
        GC.SuppressFinalize(this);
    }
}