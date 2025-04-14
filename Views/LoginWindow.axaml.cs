using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System.Collections.Generic;
using System.Diagnostics;
using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Avalonia;
using System.Security.Cryptography;
using System.Text;

namespace Aniki
{
    public partial class LoginWindow : Window
    {
        private const string ClientId = "8275007b2d810ac90b7fd2e022f5edf2";
        private const string RedirectUri = "http://localhost:8000/callback";
        private HttpListener _httpListener;
        private string _codeVerifier;

        private Button _loginButton;
        private Button _continueButton;
        private Button _logoutButton;
        private TextBlock _statusText;
        private ProgressBar _loadingIndicator;

        private StoredTokenData? _loadedTokens;
        public LoginWindow()
        {
            InitializeComponent();

#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            _loginButton = this.FindControl<Button>("LoginButton");
            _continueButton = this.FindControl<Button>("ContinueButton");
            _logoutButton = this.FindControl<Button>("LogoutButton");
            _statusText = this.FindControl<TextBlock>("StatusText");
            _loadingIndicator = this.FindControl<ProgressBar>("LoadingIndicator");

            // Attach Event Handlers
            _loginButton.Click += LoginButton_Click;
            _continueButton.Click += ContinueButton_Click;
            _logoutButton.Click += LogoutButton_Click;

            // Check login status when the window loads
            this.Loaded += LoginWindow_Loaded;
        }

        private async void LoginWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            await CheckExistingLogin();
        }

        private async Task CheckExistingLogin()
        {
            SetLoadingState(true, "Checking login status...");
            _loadedTokens = await TokenManager.LoadTokensAsync();

            if (_loadedTokens != null && !string.IsNullOrEmpty(_loadedTokens.AccessToken))
            {
                // Try to fetch username to confirm token validity
                try
                {
                    // Assuming MalUtils is accessible here or create an instance
                    var userData = await MalUtils.LoadUserData(_loadedTokens.AccessToken);
                    if (userData != null && !string.IsNullOrEmpty(userData.Name))
                    {
                        // Valid token, show continue options
                        _continueButton.Content = $"Continue as {userData.Name}";
                        SetLoggedInState(true);
                        SetLoadingState(false);
                        return; // Exit early, user is logged in
                    }
                    else
                    {
                        // Token loaded but couldn't fetch user data (maybe expired/invalid)
                        _statusText.Text = "Session expired or invalid. Please log in again.";
                        TokenManager.ClearTokens(); // Clear invalid tokens
                        _loadedTokens = null;
                    }
                }
                catch (Exception ex) // Catch specific exceptions if MalUtils throws them
                {
                    Console.WriteLine($"Failed to verify token with user data: {ex.Message}");
                    _statusText.Text = "Could not verify session. Please log in again.";
                    TokenManager.ClearTokens(); // Clear potentially invalid tokens
                    _loadedTokens = null;
                }
            }

            // No valid token found or verification failed
            SetLoggedInState(false);
            SetLoadingState(false);
        }

        // --- Button Click Handlers ---

        private async void LoginButton_Click(object? sender, RoutedEventArgs e)
        {
            SetLoadingState(true, "Starting login process...");
            await StartOAuthFlow();
            // Loading state reset happens within OAuth flow or on failure
        }

        private void ContinueButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_loadedTokens != null)
            {
                OpenMainWindow(_loadedTokens.AccessToken);
            }
            else
            {
                // Should not happen if button is visible, but handle defensively
                _statusText.Text = "Error: No session data found. Please log in.";
                SetLoggedInState(false);
                SetLoadingState(false);
            }
        }

        private void LogoutButton_Click(object? sender, RoutedEventArgs e)
        {
            TokenManager.ClearTokens();
            _loadedTokens = null;
            SetLoggedInState(false); // Show the main login button
            _statusText.Text = "Logged out. Ready to log in.";
            // Optionally: could immediately trigger StartOAuthFlow() here too
        }


        // --- OAuth Flow Logic (Mostly unchanged, but adds token saving) ---

        private async Task StartOAuthFlow()
        {
            // Hide buttons during login process
            _loginButton.IsVisible = false;
            _continueButton.IsVisible = false;
            _logoutButton.IsVisible = false;
            SetLoadingState(true, "Redirecting to MyAnimeList...");


            try
            {
                _codeVerifier = GenerateCodeVerifier();
                string codeChallenge = _codeVerifier; // Using plain for simplicity, S256 is recommended

                _httpListener = new HttpListener();
                _httpListener.Prefixes.Clear(); // Ensure it's clean before adding
                _httpListener.Prefixes.Add(RedirectUri.EndsWith("/") ? RedirectUri : RedirectUri + "/");
                _httpListener.Start();

                string authUrl = "https://myanimelist.net/v1/oauth2/authorize" +
                                 $"?response_type=code" +
                                 $"&client_id={ClientId}" +
                                 $"&code_challenge={codeChallenge}" +
                                 $"&code_challenge_method=plain" + // Use S256 in production!
                                 $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                                 $"&state={Guid.NewGuid()}"; // Add state for CSRF protection

                OpenBrowser(authUrl);
                SetLoadingState(true, "Waiting for authentication in browser...");

                HttpListenerContext context = await _httpListener.GetContextAsync();
                string? code = context.Request.QueryString["code"];
                // TODO: Verify state parameter matches the one sent

                // Send response to browser immediately
                string responseString = "<html><head><title>Auth Success</title></head><body><h1>Authentication successful!</h1><p>You can close this browser tab/window and return to Aniki.</p><script>window.close();</script></body></html>";
                byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                context.Response.ContentType = "text/html";
                context.Response.ContentLength64 = buffer.Length;
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                context.Response.Close(); // Close the response stream
                _httpListener.Stop(); // Stop listener AFTER sending response


                if (!string.IsNullOrEmpty(code))
                {
                    SetLoadingState(true, "Exchanging code for token...");
                    await ExchangeCodeForToken(code);
                }
                else
                {
                    SetLoadingState(false, "Authentication failed or cancelled.");
                    SetLoggedInState(false); // Show login button again
                }
            }
            catch (HttpListenerException hlex) when (hlex.ErrorCode == 5) // Access Denied
            {
                SetLoadingState(false, $"Error: Access denied starting listener on {RedirectUri}.\nTry running Aniki as Administrator OR use netsh http add urlacl url={RedirectUri}/ user=Everyone");
                SetLoggedInState(false);
            }
            catch (Exception ex)
            {
                SetLoadingState(false, $"Authentication error: {ex.Message}");
                SetLoggedInState(false); // Show login button again
                if (_httpListener?.IsListening ?? false) _httpListener.Stop(); // Ensure listener is stopped on error
            }
            finally
            {
                _httpListener?.Close(); // Dispose listener resources
            }
        }

        private async Task ExchangeCodeForToken(string code)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    var content = new FormUrlEncodedContent(new[]
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
                        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        if (tokenResponse != null)
                        {
                            // *** SAVE THE TOKENS SECURELY ***
                            await TokenManager.SaveTokensAsync(tokenResponse);

                            SetLoadingState(false, "Authentication successful!");
                            OpenMainWindow(tokenResponse.access_token);
                        }
                        else
                        {
                            SetLoadingState(false, "Token exchange failed: Could not parse response.");
                            SetLoggedInState(false);
                        }
                    }
                    else
                    {
                        SetLoadingState(false, $"Token exchange failed: {response.StatusCode} - {responseBody}");
                        SetLoggedInState(false);
                    }
                }
            }
            catch (Exception ex)
            {
                SetLoadingState(false, $"Token exchange error: {ex.Message}");
                SetLoggedInState(false);
            }
        }

        // --- Helper Methods ---

        private void OpenMainWindow(string accessToken)
        {
            // Ensure MalUtils is accessible (e.g., static or passed)
            var mainWindow = new MainWindow(accessToken); // Pass the token
            mainWindow.Show();
            this.Close(); // Close the login window
        }

        private void SetLoadingState(bool isLoading, string? statusMessage = null)
        {
            _loadingIndicator.IsVisible = isLoading;
            if (!string.IsNullOrEmpty(statusMessage))
            {
                _statusText.Text = statusMessage;
            }
        }

        private void SetLoggedInState(bool isLoggedIn)
        {
            _continueButton.IsVisible = isLoggedIn;
            _logoutButton.IsVisible = isLoggedIn;
            _loginButton.IsVisible = !isLoggedIn;

            if (!isLoggedIn)
            {
                _statusText.Text = "Please log in using MyAnimeList."; // Default message when logged out
            }
        }

        private void OpenBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                // Handle cases where the browser cannot be opened
                SetLoadingState(false, $"Error opening browser: {ex.Message}\nPlease manually open:\n{url}");
                SetLoggedInState(false); // Allow user to retry login maybe? Or show error
            }
        }


        // Code Verifier/Challenge generation - Use S256 in production!
        private string GenerateCodeVerifier()
        {
            // Use a cryptographically secure random number generator
            using (var rng = RandomNumberGenerator.Create())
            {
                var bytes = new byte[32]; // 32 bytes = 256 bits
                rng.GetBytes(bytes);
                // Convert to URL-safe base64 string (no padding, +, /)
                return Base64UrlEncode(bytes);
            }
        }

        // Helper for Base64 URL encoding
        private string Base64UrlEncode(byte[] input)
        {
            var output = Convert.ToBase64String(input);
            output = output.Replace('+', '-');
            output = output.Replace('/', '_');
            output = output.TrimEnd('=');
            return output;
        }
    }

    public class TokenResponse
    {
        public string access_token { get; set; }
        public string refresh_token { get; set; }
        public int expires_in { get; set; }
        public string token_type { get; set; }
    }
}