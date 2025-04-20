using Aniki.Models;
using Aniki.Services;
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
        private string _torrentSearchTerms = string.Empty;

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

        private AnimeData _selectedAnime;
        public AnimeData SelectedAnime
        {
            get => _selectedAnime;
            set
            {
                if (SetProperty(ref _selectedAnime, value))
                {
                    _ = LoadAnimeDetailsAsync(value);
                    EpisodeNumber = value?.ListStatus?.Num_Episodes_Watched + 1 ?? 1;
                }
            }
        }

        [ObservableProperty]
        private AnimeDetails _animeDetails;

        [ObservableProperty]
        private int _episodeNumber;

        [ObservableProperty]
        private ObservableCollection<NyaaTorrent> _torrentsList = new();

        public event EventHandler LogoutRequested;

        private readonly NyaaService _nyaaService = new NyaaService();

        public List<string> FilterOptions { get; } = new List<string>
        {
            "All",
            "Currently Watching",
            "Completed",
            "On Hold",
            "Dropped",
            "Plan to Watch"
        };

        public List<int> ScoreOptions { get; } = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        public MainViewModel()
        {
            _animeList = new ObservableCollection<AnimeData>();
            _selectedFilter = "All";
        }

        private async Task LoadAnimeDetailsAsync(AnimeData animeData)
        {
            if (animeData?.Node?.Id != null)
            {
                StatusMessage = $"Loading details for {animeData.Node.Title}...";
                AnimeDetails = await MalUtils.GetAnimeDetails(animeData.Node.Id);
                StatusMessage = "Details loaded";
            }
        }

        public async Task InitializeAsync()
        {
            await LoadUserDataAsync();
            await LoadAnimeListAsync(_selectedFilter);
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
                _animeList.Clear();

                var animeListData = await MalUtils.LoadAnimeList(filter);

                foreach (var anime in animeListData)
                {
                    _animeList.Add(anime);
                }

                StatusMessage = $"Loaded {_animeList.Count} anime";
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
            await LoadAnimeListAsync(_selectedFilter);
        }

        [RelayCommand]
        private void Logout()
        {
            LogoutRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        public async Task SeachTorrents()
        {
            if (SelectedAnime == null) return;

            StatusMessage = $"Searching torrents for {SelectedAnime.Node.Title} - Episode {EpisodeNumber}...";
            TorrentsList.Clear();

            var list = await _nyaaService.SearchAsync(SelectedAnime.Node.Title, EpisodeNumber);

            foreach (var t in list)
            {
                TorrentsList.Add(t);
            }

            StatusMessage = $"Found {TorrentsList.Count} torrents";
        }

        [RelayCommand]
        public void DownloadTorrent(string magnet)
        {
            Process.Start(new ProcessStartInfo(magnet) { UseShellExecute = true });
            StatusMessage = "Opening torrent...";
        }

        [RelayCommand]
        public void CopyMagnetLink(string magnet)
        {
            //todo
            StatusMessage = "Magnet link copied to clipboard";
        }

        [RelayCommand]
        public async Task Search()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                await LoadAnimeListAsync(SelectedFilter);
                return;
            }

            IsLoading = true;
            StatusMessage = $"Searching for \"{SearchQuery}\"...";

            try
            {
                var results = await MalUtils.SearchAnime(SearchQuery);
                _animeList.Clear();

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
                    _animeList.Add(newAnimeData);
                }

                StatusMessage = $"Found {_animeList.Count} results for \"{SearchQuery}\"";
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
        public async Task UpdateEpisodeCount(int change)
        {
            if (AnimeDetails?.My_List_Status == null) return;

            int newCount = AnimeDetails.My_List_Status.Num_Episodes_Watched + change;

            if (newCount < 0) newCount = 0;

            if (AnimeDetails.Num_Episodes > 0 && newCount > AnimeDetails.Num_Episodes)
            {
                newCount = AnimeDetails.Num_Episodes;
            }

            try
            {
                StatusMessage = "Updating episode count...";
                await MalUtils.UpdateAnimeStatus(AnimeDetails.Id, MalUtils.AnimeStatusField.EPISODES_WATCHED, newCount);
                AnimeDetails.My_List_Status.Num_Episodes_Watched = newCount;

                StatusMessage = "Episode count updated";

                EpisodeNumber = newCount + 1;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error updating: {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task UpdateAnimeScore(int score)
        {
            if (AnimeDetails?.My_List_Status == null) return;

            try
            {
                StatusMessage = "Updating score...";
                await MalUtils.UpdateAnimeStatus(AnimeDetails.Id, MalUtils.AnimeStatusField.SCORE, score);
                AnimeDetails.My_List_Status.Score = score;

                StatusMessage = "Episode count updated";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error updating: {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task UpdateAnimeStatus(string status)
        {
            //TODO
        }
    }
}
