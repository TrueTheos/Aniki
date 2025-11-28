using Aniki.Services.Interfaces;

namespace Aniki.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly IOAuthService _oauthService;
    private readonly ITokenService _tokenService;
    private readonly IMalService _malService;

    private bool _isLoading;
    private string _statusMessage = "";
    private bool _isLoggedIn;
    private string _username = "";

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

    public event EventHandler? NavigateToMainRequested;
    
    public LoginViewModel(IOAuthService oauthService, ITokenService tokenService, IMalService malService)
    {
        _oauthService = oauthService;
        _tokenService = tokenService;
        _malService = malService;
    }

    public async Task CheckExistingLoginAsync()
    {
        IsLoading = true;
        StatusMessage = "Checking login status...";

        try
        {
            StoredTokenData? tokenData = await _tokenService.LoadTokensAsync();

            if (tokenData != null && !string.IsNullOrEmpty(tokenData.AccessToken))
            {
                try
                {
                    MAL_UserData? userData = await _malService.GetUserDataAsync();
                    if (userData != null && !string.IsNullOrEmpty(userData.Name))
                    {
                        Username = userData.Name;
                        IsLoggedIn = true;
                        StatusMessage = $"Welcome back, {userData.Name}!";
                        await ContinueAsync();
                        return;
                    }
                }
                catch (Exception)
                {
                    _tokenService.ClearTokens();
                }
            }

            IsLoggedIn = false;
            StatusMessage = "Please log in using MyAnimeList.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        IsLoading = true;

        Progress<string> progress = new(message => StatusMessage = message);
        bool success = _oauthService != null && await _oauthService.StartOAuthFlowAsync(progress);

        if (success)
        {
            await CheckExistingLoginAsync();
            if (IsLoggedIn)
            {
                await ContinueAsync();
            }
        }
        else
        {
            IsLoading = false;
            StatusMessage = "Login failed. Please try again.";
        }
    }

    [RelayCommand]
    private Task ContinueAsync()
    {
        if (_tokenService.HasValidToken())
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
    private void ContinueWithoutLogginIn()
    {
        NavigateToMainRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void Logout()
    {
        _tokenService.ClearTokens();
        IsLoggedIn = false;
        StatusMessage = "Logged out. Ready to log in.";
    }
}