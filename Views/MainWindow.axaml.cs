using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System;
using Aniki.Services;
using Aniki.ViewModels;
using Aniki.Views;
using Avalonia;

namespace Aniki.Views
{
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            var malApiService = new MalApiService();

            _viewModel = new MainViewModel(malApiService);
            _viewModel.LogoutRequested += OnLogoutRequested;

            DataContext = _viewModel;

            this.Loaded += MainWindow_Loaded;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async void MainWindow_Loaded(object sender, EventArgs e)
        {
            await _viewModel.InitializeAsync();
        }

        private void OnLogoutRequested(object sender, EventArgs e)
        {
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }
    }
}