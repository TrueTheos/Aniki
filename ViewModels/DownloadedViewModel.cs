using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Avalonia.Controls.ApplicationLifetimes;
using System.Collections.ObjectModel;
using Aniki.Models.MAL;
using Aniki.Services.Interfaces;
using CommunityToolkit.Mvvm.Messaging;
using Aniki.Views;

namespace Aniki.ViewModels;

public partial class DownloadedViewModel : ViewModelBase
{
    private DownloadedEpisode? _lastPlayedEpisode;

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

    public DownloadedViewModel(IDiscordService discordService, IMalService malService, ISaveService saveService, IAbsoluteEpisodeParser absoluteEpisodeParser, IAnimeNameParser animeNameParser)
    {
        _discordService = discordService;
        _malService = malService;
        _saveService = saveService;
        _absoluteEpisodeParser = absoluteEpisodeParser;
        _animeNameParser = animeNameParser;
        
        IsEpisodesViewVisible = false;
        IsNoEpisodesViewVisible = true;

        _ = LoadEpisodesFromFolder();
        
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
            IsLoading = false;
            UpdateView();
            return;
        }

        var videoExtensions = new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv" };

        var filePaths = Directory.GetFiles(episodesFolder, "*.*", SearchOption.AllDirectories)
            .Where(f => videoExtensions.Contains(Path.GetExtension(f).ToLower()))
            .ToList();

        int total = filePaths.Count;
        int processed = 0;
        ProcessingProgress = $"Processing files: 0/{total}";

        var options = new ParallelOptions { MaxDegreeOfParallelism = 8 };

        await Parallel.ForEachAsync(filePaths, options, async (filePath, _) =>
        {
            string fileName = Path.GetFileName(filePath);
            var parsedFile = await _animeNameParser.ParseAnimeFilename(fileName);

            if (parsedFile.EpisodeNumber == null)
                return;
    
            int? malId = await _absoluteEpisodeParser.GetMalIdForSeason(parsedFile.AnimeName, parsedFile.Season);
            if (malId == null)
                return;

            var animeName = await _malService.GetFieldsAsync(malId.Value, AnimeField.TITLE);

            var episode = new DownloadedEpisode(filePath, int.Parse(parsedFile.EpisodeNumber ?? "0"),
                parsedFile.AbsoluteEpisodeNumber,
                animeName.Title!, malId.Value, parsedFile.Season);

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => AddEpisodeToGroup(episode));

            int current = Interlocked.Increment(ref processed);
            ProcessingProgress = $"Processing files: {current}/{total}";
        });

        IsLoading = false;
        UpdateView();
    }

    private void AddEpisodeToGroup(DownloadedEpisode downloadedEpisode)
    {
        AnimeGroup? existingGroup = AnimeGroups.FirstOrDefault(g => g.Title == downloadedEpisode.Title);
        
        if (existingGroup != null)
        {
            InsertEpisodeInSortedOrder(existingGroup.Episodes, downloadedEpisode);
        }
        else
        {
            AnimeGroup newGroup = new(downloadedEpisode.Title, new ObservableCollection<DownloadedEpisode> { downloadedEpisode });
            InsertGroupInSortedOrder(newGroup);
        }
    }

    private void InsertEpisodeInSortedOrder(ObservableCollection<DownloadedEpisode> episodes, DownloadedEpisode newDownloadedEpisode)
    {
        int insertIndex = 0;
        
        for (int i = 0; i < episodes.Count; i++)
        {
            DownloadedEpisode existingDownloadedEpisode = episodes[i];
            
            if (newDownloadedEpisode.Season < existingDownloadedEpisode.Season)
            {
                insertIndex = i;
                break;
            }
            else if (newDownloadedEpisode.Season == existingDownloadedEpisode.Season)
            {
                if (newDownloadedEpisode.EpisodeNumber < existingDownloadedEpisode.EpisodeNumber)
                {
                    insertIndex = i;
                    break;
                }
            }
            
            insertIndex = i + 1;
        }
        
        episodes.Insert(insertIndex, newDownloadedEpisode);
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
        OnPropertyChanged(nameof(AnimeGroups));
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
    private void LaunchEpisode(DownloadedEpisode ep)
    {
        string defaultApp = GetAssociatedProgram(Path.GetExtension(ep.FilePath));

        _lastPlayedEpisode = ep;

        _discordService.SetPresenceEpisode(ep);

        LaunchAndTrack(defaultApp, ep.FilePath);
    }

    [RelayCommand]
    private void DeleteEpisode(DownloadedEpisode ep)
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
        uint ret = AssocQueryString(AssocF.None, AssocStr.Executable, extension, null, null, ref length);

        if (ret != 1 && length > 0)
        {
            StringBuilder sb = new((int)length);
            ret = AssocQueryString(AssocF.None, AssocStr.Executable, extension, null, sb, ref length);

            if (ret == 0)
            {
                return sb.ToString();
            }
        }
        return "explorer.exe";
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

    private void MarkEpisodeCompleted(DownloadedEpisode ep)
    {
        int episodeToMark = ep.EpisodeNumber;
        _ = _malService.UpdateEpisodesWatched(ep.Id,  episodeToMark);
    }
}
