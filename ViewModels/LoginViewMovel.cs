using System.Windows.Input;
using Aniki.Services.Interfaces;

namespace Aniki.ViewModels;

public class LoginViewModel : ViewModelBase
{
    private readonly IOAuthService _oauthService;
    private readonly ITokenService _tokenService;
    private readonly IMalService _malService;

    private bool _isLoading;
    private string _statusMessage = "";
    private bool _isLoggedIn;
    private string _username = "";
    private ICommand? _loginCommand;
    private ICommand? _continueCommand;
    private ICommand? _logoutCommand;

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

    public ICommand LoginCommand => _loginCommand ??= new RelayCommand(async () => await LoginAsync());
    public ICommand ContinueCommand => _continueCommand ??= new RelayCommand(async () => await ContinueAsync());
    public ICommand LogoutCommand => _logoutCommand ??= new RelayCommand(LogoutAsync);

    public event EventHandler<string>? NavigateToMainRequested;
    
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

    private async Task LoginAsync()
    {
        IsLoading = true;

        Progress<string> progress = new Progress<string>(message => StatusMessage = message);
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

    private Task ContinueAsync()
    {
        if (_tokenService.HasValidToken())
        {
            NavigateToMainRequested?.Invoke(this, _tokenService.GetAccessToken());
        }
        else
        {
            StatusMessage = "Session expired. Please log in again.";
            IsLoggedIn = false;
        }

        return Task.CompletedTask;
    }

    private void LogoutAsync()
    {
        _tokenService.ClearTokens();
        IsLoggedIn = false;
        StatusMessage = "Logged out. Ready to log in.";
    }
}

public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute == null || _canExecute();
    public void Execute(object? parameter) => _execute();
    public event EventHandler? CanExecuteChanged;

    protected virtual void OnCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}