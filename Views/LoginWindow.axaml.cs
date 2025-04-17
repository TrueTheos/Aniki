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
using Aniki.Services;
using Aniki.ViewModels;

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

            var oauthService = new OAuthService();

            _viewModel = new LoginViewModel(oauthService);
            _viewModel.NavigateToMainRequested += OnNavigateToMainRequested;

            DataContext = _viewModel;

            this.Loaded += LoginWindow_Loaded;
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
            var mainWindow = new MainWindow();
            mainWindow.Show();
            this.Close();
        }
    }
}