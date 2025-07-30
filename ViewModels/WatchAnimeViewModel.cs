using Aniki.Models;
using Aniki.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Controls.ApplicationLifetimes;
using System.Collections.ObjectModel;

namespace Aniki.ViewModels
{
    public partial class WatchAnimeViewModel : ViewModelBase
    {
        private AnimeDetails? _details;

        private Episode? _lastPlayedEpisode;

        [ObservableProperty]
        private bool _isEpisodesViewVisible;

        [ObservableProperty]
        private bool _isNoEpisodesViewVisible;

        [ObservableProperty]
        private ObservableCollection<Episode> _episodes = new();

        public string EpisodesFolderMessage { get; } = $"Episodes folder is empty - {SaveService.DefaultEpisodesFolder}";

        [DllImport("Shlwapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern uint AssocQueryString(AssocF flags, AssocStr str, string pszAssoc, string? pszExtra, [Out] StringBuilder pszOut, ref uint pcchOut);

        enum AssocF
        {
            None = 0,
        }

        enum AssocStr
        {
            Executable = 2,
        }

        public WatchAnimeViewModel()
        {
            Update(null);
        }

        public void Update(AnimeDetails? details)
        {
            _details = details;

            IsEpisodesViewVisible = false;
            IsNoEpisodesViewVisible = false;
            Episodes.Clear();

            foreach (string filePath in Directory.GetFiles(SaveService.DefaultEpisodesFolder, "*.*", SearchOption.AllDirectories))
            {
                string fileName = Path.GetFileName(filePath);
                ParseResult result = AnimeNameParser.ParseAnimeFilename(fileName);
                if (result == null || result.EpisodeNumber == null) continue;

                if (_details == null) continue;
                if (FuzzySharp.Fuzz.Ratio(result.AnimeName.ToLower(), _details.Title.ToLower()) > 90)
                {
                    Episodes.Add(new(filePath, result.EpisodeNumber,
                        title: _details.Title, id: _details.Id));
                }
            }

            if (Episodes.Count > 0)
            {
                IsEpisodesViewVisible = true;
                IsNoEpisodesViewVisible = false;
            }
            else
            {
                IsNoEpisodesViewVisible = true;
                IsEpisodesViewVisible = false;
            }
        }

        [RelayCommand]
        private void LaunchEpisode(Episode ep)
        {
            string defaultApp = GetAssociatedProgram(Path.GetExtension(ep.FilePath));

            _lastPlayedEpisode = ep;

            DiscordService.SetPresenceEpisode(ep);

            LaunchAndTrack(defaultApp, ep.FilePath);
        }

        [RelayCommand]
        private void DeleteEpisode(Episode ep)
        {
            File.Delete(ep.FilePath);
            Episodes.Remove(ep);
        }

        private string GetAssociatedProgram(string extension)
        {
            uint length = 0;
            AssocQueryString(AssocF.None, AssocStr.Executable, extension, null, null, ref length);

            StringBuilder sb = new((int)length);
            AssocQueryString(AssocF.None, AssocStr.Executable, extension, null, sb, ref length);

            return sb.ToString();
        }

        private void LaunchAndTrack(string appPath, string filePath)
        {
            Process process = new()
            {
                StartInfo =
                {
                    FileName = appPath,
                    Arguments = $"\"{filePath}\""
                },
                EnableRaisingEvents = true
            };
            process.Exited += (_, _) => { OnVideoPlayerClosed(); };

            process.Start();
        }

        private void OnVideoPlayerClosed()
        {
            Console.WriteLine("Video player closed!");

            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (_lastPlayedEpisode == null) return;
                if (Avalonia.Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var dialog = new Views.ConfirmEpisodeWindow
                    {
                        DataContext = new ConfirmEpisodeViewModel(_lastPlayedEpisode.EpisodeNumber)
                    };

                    bool result = await dialog.ShowDialog<bool>(desktop.MainWindow!);

                    if (result)
                    {
                        MarkEpisodeCompleted(_lastPlayedEpisode);
                    }
                }
            });

            DiscordService.Reset();
        }

        private void MarkEpisodeCompleted(Episode ep)
        {
            _ = MalUtils.UpdateAnimeStatus(ep.Id, MalUtils.AnimeStatusField.EPISODES_WATCHED, ep.EpisodeNumber.ToString());
        }
    }

    public class Episode(string filePath, int episodeNumber, string title, int id)
    {
        public string FilePath { get; init; } = filePath;
        public int EpisodeNumber { get; init; } = episodeNumber;
        public string Title { get; init; } = title;
        public int Id { get; init; } = id;
    }
}
