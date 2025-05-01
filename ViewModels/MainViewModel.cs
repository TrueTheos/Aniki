using Aniki.Models;
using Aniki.Services;
using Aniki.Views;
using Avalonia.Data.Converters;
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

namespace Aniki.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ObservableCollection<AnimeData> _animeList;

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

        private string _selectedFilter;
        public string SelectedFilter
        {
            get => _selectedFilter;
            set
            {
                if (SetProperty(ref _selectedFilter, value))
                {
                    _ = LoadAnimeListAsync(value);
                }
            }
        }

        [ObservableProperty]
        private AnimeDetailsViewModel _animeDetailsViewModel;

        [ObservableProperty]
        private WatchAnimeViewModel _watchAnimeViewModel;

        private AnimeData _selectedAnime;
        public AnimeData SelectedAnime
        {
            get => _selectedAnime;
            set
            {
                if (SetProperty(ref _selectedAnime, value))
                {
                    _ = LoadAnimeDetailsAsync(value);
                }
            }
        }

        public event EventHandler LogoutRequested;
        public event EventHandler SettingsRequested;

        public List<string> FilterOptions { get; } = new List<string>
        {
            "All",
            "Currently Watching",
            "Completed",
            "On Hold",
            "Dropped",
            "Plan to Watch"
        };

        public MainViewModel()
        {
            AnimeDetailsViewModel = new();
            WatchAnimeViewModel = new();
            _animeList = new ObservableCollection<AnimeData>();
            SelectedFilter = "All";
        }

        private async Task LoadAnimeDetailsAsync(AnimeData animeData)
        {
            if (animeData?.Node?.Id != null)
            {
                if(AnimeDetailsViewModel == null)
                {
                    AnimeDetailsViewModel = new();
                }
                AnimeDetailsViewModel.IsLoading = true;
                AnimeDetails details = await MalUtils.GetAnimeDetails(animeData.Node.Id);
                AnimeDetailsViewModel.Update(details);
                WatchAnimeViewModel = new WatchAnimeViewModel(details);
                IsLoading = false;
            }
            else
            {
                AnimeDetailsViewModel = null;
                WatchAnimeViewModel = null;
            }
        }

        public async Task InitializeAsync()
        {
            await LoadUserDataAsync();
            await LoadAnimeListAsync(SelectedFilter);
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

        private async Task LoadAnimeListAsync(string filter)
        {
            try
            {
                IsLoading = true;
                StatusMessage = $"Loading anime list (Filter: {filter})...";
                AnimeList.Clear();

                var animeListData = await MalUtils.LoadAnimeList(filter);

                foreach (var anime in animeListData)
                {
                    AnimeList.Add(anime);
                }

                StatusMessage = $"Loaded {AnimeList.Count} anime";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading anime list: {ex.Message}");
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task Refresh()
        {
            await LoadUserDataAsync();
            await LoadAnimeListAsync(SelectedFilter);
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
                await LoadAnimeListAsync("all");
                return;
            }

            IsLoading = true;
            StatusMessage = $"Searching for \"{SearchQuery}\"...";

            try
            {
                var results = await MalUtils.SearchAnime(SearchQuery);
                AnimeList.Clear();

                foreach (var entry in results)
                {
                    if (entry == null) continue;

                    var newAnimeData = new AnimeData()
                    {
                        Node = new AnimeNode
                        {
                            Id = entry.Anime.Id,
                            Title = entry.Anime.Title
                        }
                    };
                    AnimeList.Add(newAnimeData);
                }

                StatusMessage = $"Found {AnimeList.Count} results for \"{SearchQuery}\"";
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
