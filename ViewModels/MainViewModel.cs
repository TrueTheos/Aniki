using Aniki.Misc;
using Aniki.Models;
using Aniki.Services;
using Aniki.Views;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml.Linq;
using static Aniki.Services.SaveService;

namespace Aniki.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _username;

        [ObservableProperty]
        private Bitmap _profileImage;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        public event EventHandler LogoutRequested;
        public event EventHandler SettingsRequested;

        #region Views
        [ObservableProperty]
        private ViewModelBase _currentViewModel;

        [ObservableProperty]
        private AnimeDetailsViewModel _animeDetailsViewModel;
        [ObservableProperty]
        private AnimeDetailsViewModel _watchViewModel;
        [ObservableProperty]
        private CalendarViewModel _calendarViewModel;
        [ObservableProperty]
        private StatsViewModel _statsViewModel;
        #endregion

        public MainViewModel() 
        {
            AnimeDetailsViewModel = new AnimeDetailsViewModel();
            WatchViewModel = new AnimeDetailsViewModel();
            CalendarViewModel = new CalendarViewModel();
            StatsViewModel = new StatsViewModel();
        }

        [RelayCommand]
        public void ShowMainPage()
        {
            CurrentViewModel = AnimeDetailsViewModel; _ = CurrentViewModel.Enter();
        }

        [RelayCommand]
        public void ShowWatchPage()
        {
            CurrentViewModel = AnimeDetailsViewModel; _ = CurrentViewModel.Enter();
        }

        [RelayCommand]
        public void ShowCalendarPage()
        {
            CurrentViewModel = CalendarViewModel; _ = CurrentViewModel.Enter();
        }

        [RelayCommand]
        public void ShowStatsPage()
        {
            CurrentViewModel = StatsViewModel; _ = CurrentViewModel.Enter();
        }

        public async Task InitializeAsync()
        {
            await LoadUserDataAsync();
        }

        private async Task LoadUserDataAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Loading user data...";
                var userData = await MalUtils.GetUserDataAsync();
                Username = userData.Name;
                ProfileImage = await MalUtils.GetUserPicture();
                StatusMessage = "User data loaded";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading user data: {ex.Message}");
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        [RelayCommand]
        private void Logout()
        {
            LogoutRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        public async Task Search()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                await AnimeDetailsViewModel.LoadAnimeListAsync(AnimeStatusTranslated.All);
                return;
            }

            IsLoading = true;
            StatusMessage = $"Searching for \"{SearchQuery}\"...";

            try
            {
                await AnimeDetailsViewModel.SearchAnime(SearchQuery);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error searching: {ex.Message}");
                StatusMessage = $"Search error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void OpenSettings()
        {
            SettingsRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
