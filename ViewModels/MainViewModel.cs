using Aniki.Misc;
using Aniki.Services;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
using Aniki.Models;

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
            AnimeDetailsViewModel = new();
            WatchViewModel = new();
            CalendarViewModel = new(this);
            StatsViewModel = new();
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
            SearchForAnime(title.Replace('-', ' '), true);
        }

        public void GoToAnime(int malId)
        {
            SearchForAnime(malId);
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
                UserData userData = await MalUtils.GetUserDataAsync();
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

        private async Task SearchForAnime(string searchQuery, bool showFirstBest = false)
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
                await AnimeDetailsViewModel.SearchAnimeByTitle(searchQuery, true, showFirstBest);
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

        private async Task SearchForAnime(int malId)
        {
            IsLoading = true;

            ShowMainPage();

            try
            {
                await AnimeDetailsViewModel.SearchAnimeById(malId);
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
