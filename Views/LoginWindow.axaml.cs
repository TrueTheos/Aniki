using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia;
using Aniki.Services;
using Aniki.ViewModels;
using Avalonia.Controls.ApplicationLifetimes;

namespace Aniki.Views
{
    public partial class LoginWindow : Window
    {
        private LoginViewModel _viewModel;

        public LoginWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            OAuthService oauthService = new OAuthService();

            _viewModel = new(oauthService);
            _viewModel.NavigateToMainRequested += OnNavigateToMainRequested;

            DataContext = _viewModel;

            Loaded += LoginWindow_Loaded;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.CheckExistingLoginAsync();
        }

        private void OnNavigateToMainRequested(object sender, string accessToken)
        {
            MainWindow mainWindow = new MainWindow();
            if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = mainWindow;
            }
            mainWindow.Show();
            Close();
        }
    }
}