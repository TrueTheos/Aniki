using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using Aniki.Services;
using Aniki.ViewModels;
using Aniki.Models;
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

            _viewModel = new();
            _viewModel.LogoutRequested += OnLogoutRequested;
            _viewModel.SettingsRequested += OnSettingsRequested;

            DataContext = _viewModel;

            Loaded += MainWindow_Loaded;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async void MainWindow_Loaded(object sender, EventArgs e)
        {
            await _viewModel.InitializeAsync();
            _ = SaveService.SyncAnimeWithMal();
        }

        private void OnLogoutRequested(object sender, EventArgs e)
        {
            LoginWindow loginWindow = new();
            loginWindow.Show();
            Close();
        }

        private async void OnSettingsRequested(object sender, EventArgs e)
        {
            SettingsWindow settingsWindow = new()
            {
                DataContext = new SettingsViewModel()
            };
            await settingsWindow.ShowDialog(this);
        }

        private void TodayAnime_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count <= 0) return;
            
            AnimeScheduleItem? anime = e.AddedItems[0] as AnimeScheduleItem;
            _viewModel.GoToAnime(anime.MalId ?? 0);

            if (sender is ListBox listBox)
            {
                listBox.SelectedItem = null;
            }
        }
    }
}