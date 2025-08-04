using Aniki.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;

namespace Aniki.Services
{
    public class OAuthService
    {
        private string ClientId;
        private const string RedirectUri = "http://localhost:8000/callback";

        private string _codeVerifier;
        private HttpListener _httpListener;

        public OAuthService() { }

        public async Task<bool> StartOAuthFlowAsync(IProgress<string> progressReporter)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using Stream? stream = assembly.GetManifestResourceStream("Aniki.Resources.CLIENTID.txt");
            if (stream == null)
                throw new FileNotFoundException("CLIENTID not found.");

            using StreamReader reader = new StreamReader(stream);
            ClientId = reader.ReadToEnd();

            try
            {
                _codeVerifier = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

                _httpListener = new();
                _httpListener.Prefixes.Clear();
                _httpListener.Prefixes.Add(RedirectUri.EndsWith("/") ? RedirectUri : RedirectUri + "/");
                _httpListener.Start();

                progressReporter.Report("Redirecting to MyAnimeList...");

                string authUrl = "https://myanimelist.net/v1/oauth2/authorize" +
                                 $"?response_type=code" +
                                 $"&client_id={ClientId}" +
                                 $"&code_challenge={_codeVerifier}" +
                                 $"&code_challenge_method=plain" +
                                 $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                                 $"&state={Guid.NewGuid()}";

                OpenBrowser(authUrl);
                progressReporter.Report("Waiting for authentication in browser...");

                HttpListenerContext context = await _httpListener.GetContextAsync();
                string code = context.Request.QueryString["code"];

                string responseString = "<html><head><title>Auth Success</title></head><body><h1>Authentication successful!</h1><p>You can close this browser tab/window and return to Aniki.</p><script>window.close();</script></body></html>";
                byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                context.Response.ContentType = "text/html";
                context.Response.ContentLength64 = buffer.Length;
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                context.Response.Close();
                _httpListener.Stop();

                if (!string.IsNullOrEmpty(code))
                {
                    progressReporter.Report("Exchanging code for token...");
                    return await ExchangeCodeForTokenAsync(code);
                }

                progressReporter.Report("Authentication failed or cancelled.");
                return false;
            }
            catch (HttpListenerException hlex) when (hlex.ErrorCode == 5)
            {
                progressReporter.Report($"Error: Access denied starting listener on {RedirectUri}.\nTry running Aniki as Administrator OR use netsh http add urlacl url={RedirectUri}/ user=Everyone");
                return false;
            }
            catch (Exception ex)
            {
                progressReporter.Report($"Authentication error: {ex.Message}");
                if (_httpListener?.IsListening ?? false) _httpListener.Stop();
                return false;
            }
            finally
            {
                _httpListener?.Close();
            }
        }

        private async Task<bool> ExchangeCodeForTokenAsync(string code)
        {
            try
            {
                using HttpClient client = new();
                FormUrlEncodedContent content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("client_id", ClientId),
                    new KeyValuePair<string, string>("code", code),
                    new KeyValuePair<string, string>("code_verifier", _codeVerifier),
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    new KeyValuePair<string, string>("redirect_uri", RedirectUri)
                });

                HttpResponseMessage response = await client.PostAsync("https://myanimelist.net/v1/oauth2/token", content);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    TokenResponse? tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseBody,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (tokenResponse != null)
                    {
                        await TokenService.SaveTokensAsync(tokenResponse);
                        return true;
                    }
                }

                return false;
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
}
