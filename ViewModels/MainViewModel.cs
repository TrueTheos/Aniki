using Aniki.Misc;
using Aniki.Models;
using Aniki.Services;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
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

        [ObservableProperty]
        private ObservableCollection<AnimeScheduleItem> _todayAnime = new();

        public MainViewModel() 
        {
            AnimeDetailsViewModel = new();
            WatchViewModel = new();
            CalendarViewModel = new(this);
            StatsViewModel = new();
        }

        [RelayCommand]
        public async Task ShowMainPage()
        {
            CurrentViewModel = AnimeDetailsViewModel;
            await CurrentViewModel.Enter();
        }

        [RelayCommand]
        public async Task ShowWatchPage()
        {
            CurrentViewModel = WatchViewModel;
            await CurrentViewModel.Enter();
        }

        [RelayCommand]
        public async Task ShowCalendarPage()
        {
            CurrentViewModel = CalendarViewModel; 
            await CurrentViewModel.Enter();
        }

        [RelayCommand]
        public async Task ShowStatsPage()
        {
            CurrentViewModel = StatsViewModel; 
            await CurrentViewModel.Enter();
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
            await LoadTodayAnimeAsync();
        }

        private async Task LoadTodayAnimeAsync()
        {
            var watchingList = await MalUtils.LoadAnimeList(AnimeStatusApi.watching);
            var today = await CalendarService.GetWeeklyScheduleAsync(watchingList.Select(x => x.Node.Title).ToList(), 50, DateTime.Now);

            var todaySchedule = today.FirstOrDefault(x => x.IsToday);
            if (todaySchedule == null) return;

            TodayAnime.Clear();
            foreach (var anime in todaySchedule.Items)
            {
                if(watchingList.Any(x => x.Node.Title == anime.Title))
                    TodayAnime.Add(anime);
            }
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
                await AnimeDetailsViewModel.LoadAnimeListAsync(AnimeStatusTranslated.None);
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
