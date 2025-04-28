using Aniki.Models;
using Aniki.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Controls.ApplicationLifetimes;
using Aniki.Views;
using System.Collections.ObjectModel;

namespace Aniki.ViewModels
{
    public partial class WatchAnimeViewModel : ViewModelBase
    {
        private AnimeDetails _details;

        private Episode? _lastPlayedEpisode;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private ObservableCollection<Episode> _foundEpisodes = new();

        [DllImport("Shlwapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern uint AssocQueryString(AssocF flags, AssocStr str, string pszAssoc, string pszExtra, [Out] StringBuilder pszOut, ref uint pcchOut);

        enum AssocF
        {
            None = 0,
        }

        enum AssocStr
        {
            Executable = 2,
        }

        public WatchAnimeViewModel() { }

        public WatchAnimeViewModel(AnimeDetails details)
        {
            _details = details;
            _ = LoadAnimeEpisodes();
        }

        private async Task LoadAnimeEpisodes()
        {
            if(_details == null) return;    

            IsLoading = true;
            FoundEpisodes = new();

            foreach (string filePath in Directory.GetFiles(SaveService.DefaultEpisodesFolder))
            {
                string fileName = Path.GetFileName(filePath);
                ParseResult result = AnimeNameParser.ParseAnimeFilename(fileName);
                if(result == null || result.EpisodeNumber == null) continue;

                if (FuzzySharp.Fuzz.Ratio(result.AnimeName.ToLower(), _details.Title.ToLower()) > .9)
                {
                    FoundEpisodes.Add(new Episode
                    {
                        FilePath = filePath,
                        EpisodeNumber = (int)result.EpisodeNumber,
                        Title = _details.Title,
                        Id = _details.Id
                    });
                }
            }

            IsLoading = false;
        }

        [RelayCommand]
        private void LaunchEpisode(Episode ep)
        {
            string defaultApp = GetAssociatedProgram(Path.GetExtension(ep.FilePath));

            _lastPlayedEpisode = ep;

            LaunchAndTrack(defaultApp, ep.FilePath);
        }

        [RelayCommand]
        private void DeleteEpisode(Episode ep)
        {
            File.Delete(ep.FilePath);
            FoundEpisodes.Remove(ep);
        }

        private string GetAssociatedProgram(string extension)
        {
            uint length = 0;
            AssocQueryString(AssocF.None, AssocStr.Executable, extension, null, null, ref length);

            StringBuilder sb = new StringBuilder((int)length);
            AssocQueryString(AssocF.None, AssocStr.Executable, extension, null, sb, ref length);

            return sb.ToString();
        }

        private void LaunchAndTrack(string appPath, string filePath)
        {
            Process process = new Process();
            process.StartInfo.FileName = appPath;
            process.StartInfo.Arguments = $"\"{filePath}\"";
            process.EnableRaisingEvents = true;
            process.Exited += (sender, e) =>
            {
                OnVideoPlayerClosed();
            };

            process.Start();
        }

        private void OnVideoPlayerClosed()
        {
            Console.WriteLine("Video player closed!");

            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (_lastPlayedEpisode == null) return;
                if (Avalonia.Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var dialog = new Aniki.Views.ConfirmEpisodeWindow
                    {
                        DataContext = new ConfirmEpisodeViewModel(_lastPlayedEpisode.EpisodeNumber)
                    };

                    bool result = await dialog.ShowDialog<bool>(desktop.MainWindow);

                    if (result)
                    {
                        MarkEpisodeCompleted(_lastPlayedEpisode);
                    }
                }
            });
        }

        private void MarkEpisodeCompleted(Episode ep)
        {
            _ = MalUtils.UpdateAnimeStatus(ep.Id, MalUtils.AnimeStatusField.EPISODES_WATCHED, ep.EpisodeNumber.ToString());
        }
    }

    public class Episode
    {
        public string FilePath { get; set; }
        public int EpisodeNumber { get; set; }
        public string Title { get; set; }
        public int Id { get; set; }
    }
}
