using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Avalonia.Controls.ApplicationLifetimes;
using System.Collections.ObjectModel;
using Aniki.Services.Interfaces;
using CommunityToolkit.Mvvm.Messaging;
using Aniki.Views;

namespace Aniki.ViewModels;

public partial class WatchAnimeViewModel : ViewModelBase
{
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

    [ObservableProperty]
    private string? _animeTitleFilter;

    [DllImport("Shlwapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern uint AssocQueryString(AssocF flags, AssocStr str, string pszAssoc, string? pszExtra, [Out] StringBuilder? pszOut, ref uint pcchOut);

    enum AssocF { None = 0 }

    enum AssocStr { Executable = 2 }
    
    private readonly IDiscordService _discordService;
    private readonly IMalService  _malService;
    private readonly ISaveService  _saveService;
    private readonly IAbsoluteEpisodeParser  _absoluteEpisodeParser;
    private readonly IAnimeNameParser  _animeNameParser;

    public WatchAnimeViewModel(IDiscordService discordService, IMalService malService, ISaveService saveService, IAbsoluteEpisodeParser absoluteEpisodeParser, IAnimeNameParser animeNameParser)
    {
        _discordService = discordService;
        _malService = malService;
        _saveService = saveService;
        _absoluteEpisodeParser = absoluteEpisodeParser;
        _animeNameParser = animeNameParser;
        
        IsEpisodesViewVisible = false;
        IsNoEpisodesViewVisible = true;
        
        WeakReferenceMessenger.Default.Register<SettingsChangedMessage>(this, (r, m) =>
        {
            _ = LoadEpisodesFromFolder();
        });
    }

    public override async Task Enter()
    {
        ClearFilter();
        await LoadEpisodesFromFolder();
    }

    public void ClearFilter()
    {
        AnimeTitleFilter = null;
    }

    private async Task LoadEpisodesFromFolder()
    {
        IsLoading = true;
        AnimeGroups.Clear();
        ProcessingProgress = "Processing files: 0/0";

        SettingsConfig? config = _saveService.GetSettingsConfig();
        string episodesFolder = config?.EpisodesFolder ?? _saveService.DefaultEpisodesFolder;
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

        foreach (string filePath in filePaths)
        {
            string fileName = Path.GetFileName(filePath);
            ParseResult parsedFile = await _animeNameParser.ParseAnimeFilename(fileName);
            processedFilesCount++;
            ProcessingProgress = $"Processing files: {processedFilesCount}/{filePaths.Count}";
            
            if (parsedFile.EpisodeNumber == null) continue;

            if (!string.IsNullOrEmpty(AnimeTitleFilter) && !parsedFile.AnimeName.Contains(AnimeTitleFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int? malId = await _absoluteEpisodeParser.GetMalIdForSeason(parsedFile.AnimeName, parsedFile.Season);
            animeMalIds.TryAdd(parsedFile.AnimeName, malId);
            if (malId != null)
            {
                Episode newEpisode = new(parsedFile.FileName, int.Parse(parsedFile.EpisodeNumber ?? "0"),
                    parsedFile.AbsoluteEpisodeNumber,
                    parsedFile.AnimeName, malId.Value, parsedFile.Season);
                
                AddEpisodeToGroup(newEpisode);
                UpdateView();
            }
        }

        IsLoading = false;
    }

    private void AddEpisodeToGroup(Episode episode)
    {
        AnimeGroup? existingGroup = AnimeGroups.FirstOrDefault(g => g.Title == episode.Title);
        
        if (existingGroup != null)
        {
            InsertEpisodeInSortedOrder(existingGroup.Episodes, episode);
        }
        else
        {
            AnimeGroup newGroup = new(episode.Title, new ObservableCollection<Episode> { episode });
            InsertGroupInSortedOrder(newGroup);
        }
    }

    private void InsertEpisodeInSortedOrder(ObservableCollection<Episode> episodes, Episode newEpisode)
    {
        int insertIndex = 0;
        
        for (int i = 0; i < episodes.Count; i++)
        {
            Episode existingEpisode = episodes[i];
            
            if (newEpisode.Season < existingEpisode.Season)
            {
                insertIndex = i;
                break;
            }
            else if (newEpisode.Season == existingEpisode.Season)
            {
                if (newEpisode.EpisodeNumber < existingEpisode.EpisodeNumber)
                {
                    insertIndex = i;
                    break;
                }
            }
            
            insertIndex = i + 1;
        }
        
        episodes.Insert(insertIndex, newEpisode);
    }

    private void InsertGroupInSortedOrder(AnimeGroup newGroup)
    {
        int insertIndex = 0;
        
        for (int i = 0; i < AnimeGroups.Count; i++)
        {
            if (string.Compare(newGroup.Title, AnimeGroups[i].Title, StringComparison.OrdinalIgnoreCase) < 0)
            {
                insertIndex = i;
                break;
            }
            insertIndex = i + 1;
        }
        
        AnimeGroups.Insert(insertIndex, newGroup);
    }

    public void Update(AnimeDetails? details)
    {
        AnimeTitleFilter = details?.Title;
        _ = LoadEpisodesFromFolder();
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

        _discordService.SetPresenceEpisode(ep);

        LaunchAndTrack(defaultApp, ep.FilePath);
    }

    [RelayCommand]
    private void DeleteEpisode(Episode ep)
    {
        File.Delete(ep.FilePath);
        
        var group = AnimeGroups.FirstOrDefault(g => g.Episodes.Contains(ep));
        if (group != null)
        {
            group.Episodes.Remove(ep);
            
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
        Log.Information("Video player closed!");

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

        _discordService.Reset();
    }

    private void MarkEpisodeCompleted(Episode ep)
    {
        int episodeToMark = ep.EpisodeNumber;
        _ = _malService.UpdateAnimeStatus(ep.Id, MalService.AnimeStatusField.EPISODES_WATCHED, episodeToMark.ToString());
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
