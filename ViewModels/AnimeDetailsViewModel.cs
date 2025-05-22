using Aniki.Misc;
using Aniki.Models;
using Aniki.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Aniki.Services.SaveService;

namespace Aniki.ViewModels
{
    public partial class AnimeDetailsViewModel : ViewModelBase
    {
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

        [ObservableProperty]
        private AnimeDetails _details;

        [ObservableProperty]
        private ObservableCollection<AnimeData> _animeList;

        [ObservableProperty]
        private int _episodesWatched;

        private int _nextEpisodeNumber = -1;

        [ObservableProperty]
        private bool _isLoading;

        public int NextEpisodeNumber
        {
            get => _nextEpisodeNumber == -1 ? EpisodesWatched + 1 : _nextEpisodeNumber;
            set
            {
                if (_nextEpisodeNumber != value) 
                {
                    _nextEpisodeNumber = value;
                    OnPropertyChanged(nameof(NextEpisodeNumber));
                }
            }
        }

        [ObservableProperty]
        private string _torrentSearchTerms = string.Empty;

        [ObservableProperty]
        private ObservableCollection<NyaaTorrent> _torrentsList = new();

        private int _selectedScore;
        public int SelectedScore
        {
            get => _selectedScore;
            set
            {
                if (SetProperty(ref _selectedScore, value))
                {
                    _ = UpdateAnimeScore(value.ToString());
                }
            }
        }

        private AnimeStatusTranslated _selectedStatus;
        public AnimeStatusTranslated SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                if (SetProperty(ref _selectedStatus, value))
                {
                    _ = UpdateAnimeStatus(value);
                }
            }
        }

        private AnimeStatusTranslated _selectedFilter;
        public AnimeStatusTranslated SelectedFilter
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

        public IReadOnlyList<AnimeStatusTranslated> StatusOptions { get; } = StatusEnum.TranslatedStatusOptions;

        public IEnumerable<AnimeStatusTranslated> FilterOptions => StatusEnum.TranslatedStatusOptions;

        public List<int> ScoreOptions { get; } = new List<int> {0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        [ObservableProperty]
        public ObservableCollection<int> _watchEpisodesOptions = new();

        [ObservableProperty]
        private WatchAnimeViewModel _watchAnimeViewModel;

        public AnimeDetailsViewModel() 
        { 
            WatchAnimeViewModel = new();
            _animeList = new ObservableCollection<AnimeData>();
            SelectedFilter = AnimeStatusTranslated.All;
        }

        public override async Task Enter()
        {
            await LoadAnimeListAsync(AnimeStatusTranslated.All);
        }

        public void Update(AnimeDetails details)
        {
            IsLoading = false;
            Details = details;
            EpisodesWatched = details.MyListStatus?.NumEpisodesWatched ?? 0;

            WatchEpisodesOptions = new();
            for (int i = 0; i < details.NumEpisodes; i++)
            {
                WatchEpisodesOptions.Add(i + 1);
            }
            OnPropertyChanged(nameof(EpisodesWatched));
            OnPropertyChanged(nameof(NextEpisodeNumber));
            SelectedScore = details.MyListStatus?.Score ?? 1;
            SelectedStatus = details.MyListStatus != null ? details.MyListStatus.Status.APIToTranslated() : AnimeStatusTranslated.All;
            WatchAnimeViewModel = new WatchAnimeViewModel(details);
        }

        private async Task LoadAnimeDetailsAsync(AnimeData animeData)
        {
            if (animeData?.Node?.Id != null)
            {
                IsLoading = true;
                AnimeDetails details = await MalUtils.GetAnimeDetails(animeData.Node.Id);
                Update(details);

                IsLoading = false;
            }
        }

        public async Task LoadAnimeListAsync(AnimeStatusTranslated filter)
        {
            try
            {
                IsLoading = true;
                AnimeList.Clear();

                var animeListData = await MalUtils.LoadAnimeList(filter.TranslatedToAPI());

                foreach (var anime in animeListData)
                {
                    AnimeList.Add(anime);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading anime list: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task SearchAnime(string searchQuery)
        {
            try
            {
                var results = await MalUtils.SearchAnime(searchQuery);
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error searching anime: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task UpdateEpisodeCount(int change)
        {
            if (Details?.MyListStatus == null) return;

            int newCount = EpisodesWatched + change;

            if (newCount < 0) newCount = 0;

            if (Details.NumEpisodes > 0 && newCount > Details.NumEpisodes)
            {
                newCount = Details.NumEpisodes;
            }

            try
            {
                await MalUtils.UpdateAnimeStatus(Details.Id, MalUtils.AnimeStatusField.EPISODES_WATCHED, newCount.ToString());

                EpisodesWatched = newCount;
            }
            catch (Exception ex) { }
        }

        private async Task UpdateAnimeScore(string score)
        {
            if (Details?.MyListStatus == null) return;
            if (score == null) return;

            try
            {
                await MalUtils.UpdateAnimeStatus(Details.Id, MalUtils.AnimeStatusField.SCORE, score.ToString());
                Details.MyListStatus.Score = int.Parse(score);

            }
            catch (Exception ex) { }
        }

        private async Task UpdateAnimeStatus(AnimeStatusTranslated status)
        {
            if (Details?.MyListStatus == null) return;

            try
            {
                await MalUtils.UpdateAnimeStatus(Details.Id, MalUtils.AnimeStatusField.STATUS, status.TranslatedToAPI().ToString());
                Details.MyListStatus.Status = status.TranslatedToAPI();
            }
            catch (Exception ex) { }
        }

        [RelayCommand]
        public async Task SearchTorrents()
        {
            if (Details == null) return;

            TorrentsList.Clear();

            var list = await NyaaService.SearchAsync(Details.Title, NextEpisodeNumber);

            foreach (var t in list)
            {
                TorrentsList.Add(t);
            }
        }

        [RelayCommand]
        public void DownloadTorrent(string magnet)
        {
            Process.Start(new ProcessStartInfo(magnet) { UseShellExecute = true });
        }
    }
}
