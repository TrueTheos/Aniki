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
        private int _episodeNumber;

        [ObservableProperty]
        private string _torrentSearchTerms = string.Empty;

        [ObservableProperty]
        private ObservableCollection<NyaaTorrent> _torrentsList = new();

        public AnimeDetailsViewModel() { }

        public AnimeDetailsViewModel(AnimeDetails details)
        {
            Details = details;
        }

        [RelayCommand]
        public async Task UpdateEpisodeCount(int change)
        {
            if (_details?.My_List_Status == null) return;

            int newCount = _details.My_List_Status.Num_Episodes_Watched + change;

            if (newCount < 0) newCount = 0;

            if (_details.Num_Episodes > 0 && newCount > _details.Num_Episodes)
            {
                newCount = _details.Num_Episodes;
            }

            try
            {
                await MalUtils.UpdateAnimeStatus(_details.Id, MalUtils.AnimeStatusField.EPISODES_WATCHED, newCount);
                _details.My_List_Status.Num_Episodes_Watched = newCount;

                EpisodeNumber = newCount + 1;
            }
            catch (Exception ex)
            {
            }
        }

        [RelayCommand]
        public async Task UpdateAnimeScore(int score)
        {
            if (_details?.My_List_Status == null) return;

            try
            {
                await MalUtils.UpdateAnimeStatus(_details.Id, MalUtils.AnimeStatusField.SCORE, score);
                _details.My_List_Status.Score = score;

            }
            catch (Exception ex)
            {
            }
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

            var list = await NyaaService.SearchAsync(selectedAnime.Node.Title, EpisodeNumber);

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
