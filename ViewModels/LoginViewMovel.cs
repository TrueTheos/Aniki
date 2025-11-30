using Aniki.Services.Auth;
using Aniki.Services.Interfaces;

namespace Aniki.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly ILoginService _loginService;
    
    public IReadOnlyList<ILoginProvider> LoginProviders => _loginService.Providers;

    private ILoginProvider? _currentProvider;
    private bool _isLoading;
    private string _statusMessage = "";
    private bool _isLoggedIn;
    private string _username = "";
    private bool _isTokenInputVisible;
    private string _token = "";

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsLoggedIn
    {
        get => _isLoggedIn;
        set => SetProperty(ref _isLoggedIn, value);
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }
    
    public bool IsTokenInputVisible
    {
        get => _isTokenInputVisible;
        set => SetProperty(ref _isTokenInputVisible, value);
    }

    public string Token
    {
        get => _token;
        set => SetProperty(ref _token, value);
    }

    public event EventHandler? NavigateToMainRequested;
    
    public LoginViewModel(ILoginService loginService)
    {
        _loginService = loginService;
    }

    public async Task CheckExistingLoginAsync()
    {
        IsLoading = true;
        StatusMessage = "Checking login status...";

        try
        {
            foreach (var provider in _loginService.Providers)
            {
                StatusMessage = $"Checking login status for {provider.Provider}...";
                string? username = await provider.CheckExistingLoginAsync();
                
                if (username == null) continue;
                
                _currentProvider = provider;
                Username = username;
                IsLoggedIn = true;
                StatusMessage = $"Welcome back, {username} (via {provider.Provider})!";
                await ContinueAsync();
                return;
            }

            IsLoggedIn = false;
            StatusMessage = "Please log in using one of the available services.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoginAsync(ILoginProvider.ProviderType providerId)
    {
        ILoginProvider? provider = _loginService.GetProvider(providerId);
        if (provider == null)
        {
            StatusMessage = $"Error: Unknown login provider '{providerId}'.";
            return;
        }

        IsLoading = true;
        _currentProvider = provider;
        Progress<string> progress = new(message => StatusMessage = message);
        
        if (provider.Provider == ILoginProvider.ProviderType.AniList)
        {
            IsTokenInputVisible = true;
            StatusMessage = "Please paste your AniList token below.";
            // The LoginAsync for anilist just gives instructions, no username is returned
            await provider.LoginAsync(progress);
            IsLoading = false;
        }
        else
        {
            string? username = await provider.LoginAsync(progress);

            if (username != null)
            {
                Username = username;
                IsLoggedIn = true;
                StatusMessage = $"Successfully logged in as {username} (via {provider.Provider})!";
                await ContinueAsync();
            }
            else
            {
                IsLoading = false;
                StatusMessage = $"Login failed for {provider.Provider}. Please try again.";
                _currentProvider = null;
            }
        }
    }

    [RelayCommand]
    private async Task SubmitToken()
    {
        if (_currentProvider == null || string.IsNullOrWhiteSpace(Token))
        {
            StatusMessage = "Please enter a token.";
            return;
        }

        IsLoading = true;
        await _currentProvider.SaveTokenAsync(Token);
        
        string? username = await _currentProvider.CheckExistingLoginAsync();
        if (username != null)
        {
            Username = username;
            IsLoggedIn = true;
            StatusMessage = $"Successfully logged in as {username} (via {_currentProvider.Provider})!";
            IsTokenInputVisible = false;
            await ContinueAsync();
        }
        else
        {
            IsLoading = false;
            StatusMessage = "Invalid token. Please try again.";
        }
    }

    [RelayCommand]
    private Task ContinueAsync()
    {
        if (_currentProvider != null)
        {
            NavigateToMainRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            StatusMessage = "Session expired. Please log in again.";
            IsLoggedIn = false;
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private void ContinueWithoutLoggingIn()
    {
        NavigateToMainRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void Logout()
    {
        if (_currentProvider == null) return;
        
        _currentProvider.Logout();
        IsLoggedIn = false;
        IsTokenInputVisible = false;
        _currentProvider = null;
        StatusMessage = "Logged out. Ready to log in.";
    }
}