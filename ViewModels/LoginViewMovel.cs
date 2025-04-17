using Aniki.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Aniki.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private readonly OAuthService _oauthService;
        private readonly IMalApiService _malApiService;

        private bool _isLoading;
        private string _statusMessage;
        private bool _isLoggedIn;
        private string _username;
        private ICommand _loginCommand;
        private ICommand _continueCommand;
        private ICommand _logoutCommand;

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

        public event EventHandler<string> NavigateToMainRequested;

        public LoginViewModel(OAuthService oauthService, IMalApiService malApiService)
        {
            _oauthService = oauthService;
            _malApiService = malApiService;
        }

        public async Task CheckExistingLoginAsync()
        {
            IsLoading = true;
            StatusMessage = "Checking login status...";

            try
            {
                var tokenData = await TokenService.LoadTokensAsync();

                if (tokenData != null && !string.IsNullOrEmpty(tokenData.AccessToken))
                {
                    try
                    {
                        var userData = await _malApiService.GetUserDataAsync();
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
                        TokenService.ClearTokens();
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

            var progress = new Progress<string>(message => StatusMessage = message);
            bool success = await _oauthService.StartOAuthFlowAsync(progress);

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

        private async Task ContinueAsync()
        {
            if (TokenService.HasValidToken())
            {
                NavigateToMainRequested?.Invoke(this, TokenService.GetAccessToken());
            }
            else
            {
                StatusMessage = "Session expired. Please log in again.";
                IsLoggedIn = false;
            }
        }

        private void LogoutAsync()
        {
            TokenService.ClearTokens();
            IsLoggedIn = false;
            StatusMessage = "Logged out. Ready to log in.";
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute();
        public void Execute(object parameter) => _execute();
        public event EventHandler CanExecuteChanged;
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
