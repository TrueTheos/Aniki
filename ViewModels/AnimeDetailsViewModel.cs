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

namespace Aniki.ViewModels
{
    public partial class AnimeDetailsViewModel : ViewModelBase
    {
        [ObservableProperty]
        private AnimeDetails _details;

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

        private string _selectedStatus;
        public string SelectedStatus
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

        public List<int> ScoreOptions { get; } = new List<int> {0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        public List<string> StatusOptions { get; } = new List<string>
        {
            "Watching",
            "Completed",
            "On hold",
            "Dropped",
            "Plan to watch",
        };

        public AnimeDetailsViewModel() { }

        public AnimeDetailsViewModel(AnimeDetails details)
        {
            IsLoading = false;
            Details = details;
            EpisodesWatched = details.MyListStatus?.NumEpisodesWatched ?? 0;
            SelectedScore = details.MyListStatus?.Score ?? 1;
            SelectedStatus = details.MyListStatus?.Status ?? "Plan to watch";        
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

        private async Task UpdateAnimeStatus(string status)
        {
            if (Details?.MyListStatus == null) return;

            try
            {
                string translatedStauts = status switch
                {
                    "Watching" => "watching",
                    "Completed" => "completed",
                    "On hold" => "on_hold",
                    "Dropped" => "dropped",
                    "Plan to watch" => "plan_to_watch",
                    _ => throw new ArgumentException("Invalid status")
                };

                await MalUtils.UpdateAnimeStatus(Details.Id, MalUtils.AnimeStatusField.STATUS, translatedStauts);
                Details.MyListStatus.Status = status;
            }
            catch (Exception ex) { }
        }

        [RelayCommand]
        public async Task SearchTorrents(AnimeData selectedAnime)
        {
            if (selectedAnime == null) return;

            TorrentsList.Clear();

            var list = await NyaaService.SearchAsync(selectedAnime.Node.Title, NextEpisodeNumber);

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
