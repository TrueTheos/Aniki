using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Avalonia.Controls.ApplicationLifetimes;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using Aniki.Views;

namespace Aniki.ViewModels;

public partial class WatchAnimeViewModel : ViewModelBase
{
    private readonly AnimeNameParser _animeNameParser = new();

    private Episode? _lastPlayedEpisode;

    [ObservableProperty]
    private bool _isEpisodesViewVisible;

    [ObservableProperty]
    private bool _isNoEpisodesViewVisible;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _processingProgress = "";

    [ObservableProperty]
    private ObservableCollection<AnimeGroup> _animeGroups = new();

    [ObservableProperty]
    private string _episodesFolderMessage = "";

    [DllImport("Shlwapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern uint AssocQueryString(AssocF flags, AssocStr str, string pszAssoc, string? pszExtra, [Out] StringBuilder? pszOut, ref uint pcchOut);

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
        IsEpisodesViewVisible = false;
        IsNoEpisodesViewVisible = true;
        
        WeakReferenceMessenger.Default.Register<SettingsChangedMessage>(this, (r, m) =>
        {
            _ = LoadEpisodesFromFolder();
        });
    }

    public override async Task Enter()
    {
        await LoadEpisodesFromFolder();
    }

    private async Task LoadEpisodesFromFolder()
    {
        IsLoading = true;
        AnimeGroups.Clear();
        ProcessingProgress = "Processing files: 0/0";

        SettingsConfig? config = SaveService.GetSettingsConfig();
        string episodesFolder = config?.EpisodesFolder ?? SaveService.DefaultEpisodesFolder;
        EpisodesFolderMessage = $"Episodes folder is empty - {episodesFolder}";

        if (!Directory.Exists(episodesFolder))
        {
            UpdateView();
            IsLoading = false;
            return;
        }

        string[] videoExtensions = { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv" };
        List<string> filePaths = Directory.GetFiles(episodesFolder, "*.*", SearchOption.AllDirectories)
                                     .Where(f => videoExtensions.Contains(Path.GetExtension(f).ToLower()))
                                     .ToList();

        int processedFilesCount = 0;
        Dictionary<string, int?> animeMalIds = new();
        List<Episode> allEpisodes = new();

        foreach (string filePath in filePaths)
        {
            string fileName = Path.GetFileName(filePath);
            ParseResult parsedFile = await _animeNameParser.ParseAnimeFilename(fileName);
            processedFilesCount++;
            ProcessingProgress = $"Processing files: {processedFilesCount}/{filePaths.Count}";
            
            if (parsedFile.EpisodeNumber == null) continue;
            int? malId = await AbsoluteEpisodeParser.GetMalIdForSeason(parsedFile.AnimeName, parsedFile.Season);
            animeMalIds.TryAdd(parsedFile.AnimeName, malId);
            if (malId != null)
            {
                allEpisodes.Add(new(parsedFile.FileName, int.Parse(parsedFile.EpisodeNumber ?? "0"),
                    parsedFile.AbsoluteEpisodeNumber,
                    parsedFile.AnimeName, malId.Value, parsedFile.Season));
            }
        }

        // Group episodes by anime title and create sorted groups
        var groupedEpisodes = allEpisodes
            .GroupBy(ep => ep.Title)
            .OrderBy(g => g.Key) // Sort anime titles alphabetically
            .ToList();

        foreach (var group in groupedEpisodes)
        {
            // Sort episodes within each group by Season first, then by Episode number
            var sortedEpisodes = group
                .OrderBy(ep => ep.Season)
                .ThenBy(ep => ep.EpisodeNumber)
                .ToList();

            AnimeGroups.Add(new AnimeGroup(group.Key, new ObservableCollection<Episode>(sortedEpisodes)));
        }

        UpdateView();
        IsLoading = false;
    }

    public void Update(AnimeDetails? details)
    {
        IsEpisodesViewVisible = false;
        IsNoEpisodesViewVisible = false;
        AnimeGroups.Clear();

        UpdateView();
    }
        
    private void UpdateView()
    {
        if (AnimeGroups.Count > 0)
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
        
        // Find and remove episode from the appropriate group
        var group = AnimeGroups.FirstOrDefault(g => g.Episodes.Contains(ep));
        if (group != null)
        {
            group.Episodes.Remove(ep);
            
            // Remove the group if it has no episodes left
            if (group.Episodes.Count == 0)
            {
                AnimeGroups.Remove(group);
            }
        }
        
        UpdateView();
    }

    private string GetAssociatedProgram(string extension)
    {
        uint length = 0;
        AssocQueryString(AssocF.None, AssocStr.Executable, extension, null, null, ref length);

        StringBuilder? sb = new((int)length);
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
                ConfirmEpisodeWindow dialog = new()
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
        int episodeToMark = ep.EpisodeNumber;
        _ = MalUtils.UpdateAnimeStatus(ep.Id, MalUtils.AnimeStatusField.EPISODES_WATCHED, episodeToMark.ToString());
    }
}

public class AnimeGroup
{
    public string Title { get; }
    public ObservableCollection<Episode> Episodes { get; }
    public int TotalEpisodes => Episodes.Count;
    
    public AnimeGroup(string title, ObservableCollection<Episode> episodes)
    {
        Title = title;
        Episodes = episodes;
    }
}

public class Episode
{
    public string FilePath { get; }
    public int EpisodeNumber { get; }
    public int? AbsoluteEpisodeNumber { get; }
    public int Season { get; }
    public string Title { get; }
    public int Id { get;  }

    public Episode(string filePath, int episodeNumber, int? absoluteEpisodeNumber, string title, int id, int season)
    {
        FilePath = filePath;
        EpisodeNumber = episodeNumber;
        AbsoluteEpisodeNumber = absoluteEpisodeNumber;
        Title = title;
        Id = id;
        Season = season;    
    }
}