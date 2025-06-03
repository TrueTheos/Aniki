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

        public event EventHandler LogoutRequested;
        public event EventHandler SettingsRequested;

        #region Views
        [ObservableProperty]
        private ViewModelBase _currentViewModel;

        [ObservableProperty]
        private AnimeDetailsViewModel _animeDetailsViewModel;
        [ObservableProperty]
        private WatchAnimeViewModel _watchViewModel;
        [ObservableProperty]
        private CalendarViewModel _calendarViewModel;
        [ObservableProperty]
        private StatsViewModel _statsViewModel;
        #endregion

        public MainViewModel() 
        {
            AnimeDetailsViewModel = new AnimeDetailsViewModel();
            WatchViewModel = new WatchAnimeViewModel();
            CalendarViewModel = new CalendarViewModel(this);
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
            CurrentViewModel = WatchViewModel; _ = CurrentViewModel.Enter();
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

        public void GoToAnime(string title)
        {
            SearchForAnime(title);
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
                var userData = await MalUtils.GetUserDataAsync();
                Username = userData.Name;
                ProfileImage = await MalUtils.GetUserPicture();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading user data: {ex.Message}");
            }
            finally
            {
                ShowMainPage();
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
            await SearchForAnime(SearchQuery);
        }

        private async Task SearchForAnime(string searchQuery)
        {
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                await AnimeDetailsViewModel.LoadAnimeListAsync(AnimeStatusTranslated.All);
                return;
            }

            IsLoading = true;

            ShowMainPage();

            try
            {
                await AnimeDetailsViewModel.SearchAnime(searchQuery);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error searching: {ex.Message}");
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
