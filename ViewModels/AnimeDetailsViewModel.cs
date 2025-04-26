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

        public int NextEpisodeNumber => EpisodesWatched + 1;

        [ObservableProperty]
        private string _torrentSearchTerms = string.Empty;

        [ObservableProperty]
        private ObservableCollection<NyaaTorrent> _torrentsList = new();
        public List<int> ScoreOptions { get; } = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        public AnimeDetailsViewModel() { }

        public AnimeDetailsViewModel(AnimeDetails details)
        {
            Details = details;
            EpisodesWatched = details.MyListStatus?.NumEpisodesWatched ?? 0;
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
                await MalUtils.UpdateAnimeStatus(Details.Id, MalUtils.AnimeStatusField.EPISODES_WATCHED, newCount);

                EpisodesWatched = newCount;
            }
            catch (Exception ex) { }
        }

        [RelayCommand]
        public async Task UpdateAnimeScore(int score)
        {
            if (Details?.MyListStatus == null) return;

            try
            {
                await MalUtils.UpdateAnimeStatus(Details.Id, MalUtils.AnimeStatusField.SCORE, score);
                Details.MyListStatus.Score = score;

            }
            catch (Exception ex) { }
        }

        [RelayCommand]
        public async Task UpdateAnimeStatus(string status)
        {
            //TODO
        }

        [RelayCommand]
        public async Task SearchTorrents(AnimeData selectedAnime)
        {
            if (selectedAnime == null) return;

            TorrentsList.Clear();

            var list = await NyaaService.SearchAsync(selectedAnime.Node.Title, EpisodesWatched + 1);

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

        [RelayCommand]
        public void CopyMagnetLink(string magnet)
        {
            //todo
        }
    }
}
