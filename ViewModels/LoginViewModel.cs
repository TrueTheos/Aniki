using Aniki.Services.Anime;
using Aniki.Services.Auth;

namespace Aniki.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly ILoginService _loginService;
    
    public IReadOnlyList<ILoginProvider> LoginProviders => _loginService.Providers;

    private ILoginProvider? _currentProvider;

    public bool IsLoading
    {
        get;
        set => SetProperty(ref field, value);
    }

    public string StatusMessage
    {
        get;
        set => SetProperty(ref field, value);
    } = "";

    public bool IsLoggedIn
    {
        get;
        set => SetProperty(ref field, value);
    }

    public string Username
    {
        get;
        set => SetProperty(ref field, value);
    } = "";

    public bool IsTokenInputVisible
    {
        get;
        set => SetProperty(ref field, value);
    }

    public string Token
    {
        get;
        set => SetProperty(ref field, value);
    } = "";

    public event EventHandler? NavigateToMainRequested;

    private readonly IAnimeService _animeService;
    
    public LoginViewModel(ILoginService loginService, IAnimeService animeService)
    {
        _loginService = loginService;
        _animeService = animeService;
    }

    public async Task CheckExistingLoginAsync()
    {
        IsLoading = true;
        StatusMessage = "Checking login status...";

        try
        {
            foreach (ILoginProvider provider in _loginService.Providers)
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
        _animeService.SetActiveProvider(ILoginProvider.ProviderType.Mal, null);
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