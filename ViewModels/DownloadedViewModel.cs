using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Avalonia.Controls.ApplicationLifetimes;
using System.Collections.ObjectModel;
using Aniki.Services.Anime;
using Aniki.Services.Interfaces;
using CommunityToolkit.Mvvm.Messaging;
using Aniki.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Aniki.ViewModels;

public partial class DownloadedViewModel : ViewModelBase, IDisposable
{
    private DownloadedEpisode? _lastPlayedEpisode;
    private FileSystemWatcher? _fileWatcher;
    private System.Timers.Timer? _debounceTimer;
    private readonly object _debounceLock = new();

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
    private readonly IAnimeService  _animeService;
    private readonly ISaveService  _saveService;
    private readonly IAbsoluteEpisodeParser  _absoluteEpisodeParser;
    private readonly IAnimeNameParser  _animeNameParser;
    private readonly IVideoPlayerService _videoPlayerService;

    public VideoPlayerOption? SelectedPlayer
    {
        get => _videoPlayerService.SelectedPlayer;
        set
        {
            if (_videoPlayerService.SelectedPlayer != value)
            {
                _videoPlayerService.SelectedPlayer = value;
                OnPropertyChanged();
            }
        }
    }

    public ObservableCollection<VideoPlayerOption> AvailablePlayers => _videoPlayerService.AvailablePlayers;

    public DownloadedViewModel(IDiscordService discordService, IAnimeService animeService, ISaveService saveService, 
        IAbsoluteEpisodeParser absoluteEpisodeParser, IAnimeNameParser animeNameParser, IVideoPlayerService videoPlayerService)
    {
        _discordService = discordService;
        _animeService = animeService;
        _saveService = saveService;
        _absoluteEpisodeParser = absoluteEpisodeParser;
        _animeNameParser = animeNameParser;
        _videoPlayerService = videoPlayerService;
        
        IsEpisodesViewVisible = false;
        IsNoEpisodesViewVisible = true;

        _ = LoadEpisodesFromFolder();
        SetupFileWatcher();
        
        WeakReferenceMessenger.Default.Register<SettingsChangedMessage>(this, (r, m) =>
        {
            SetupFileWatcher();
            _ = LoadEpisodesFromFolder();
        });
    }

    private void SetupFileWatcher()
    {
        _fileWatcher?.Dispose();
        _debounceTimer?.Dispose();

        SettingsConfig? config = _saveService.GetSettingsConfig();
        string episodesFolder = config?.EpisodesFolder ?? _saveService.DefaultEpisodesFolder;

        if (!Directory.Exists(episodesFolder))
        {
            try
            {
                Directory.CreateDirectory(episodesFolder);
            }
            catch
            {
                return;
            }
        }

        _debounceTimer = new System.Timers.Timer(500);
        _debounceTimer.AutoReset = false;
        _debounceTimer.Elapsed += async (s, e) => await LoadEpisodesFromFolder();

        _fileWatcher = new FileSystemWatcher(episodesFolder)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            Filter = "*.*",
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };

        _fileWatcher.Created += OnFileChanged;
        _fileWatcher.Deleted += OnFileChanged;
        _fileWatcher.Renamed += OnFileChanged;
        _fileWatcher.Changed += OnFileChanged;
        _fileWatcher.Error += OnWatcherError;
    }

    [RelayCommand]
    public void OpenAnimeDetails(int malId)
    {
        MainViewModel vm = App.ServiceProvider.GetRequiredService<MainViewModel>();
        vm.GoToAnime(malId);
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        var videoExtensions = new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv" };
        if (!videoExtensions.Contains(Path.GetExtension(e.Name)?.ToLower()))
            return;

        lock (_debounceLock)
        {
            _debounceTimer?.Stop();
            _debounceTimer?.Start();
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        Log.Error(e.GetException(), "FileSystemWatcher error occurred");
        
        Task.Delay(2000).ContinueWith(_ => SetupFileWatcher());
    }

    public override async Task Enter()
    {
        ClearFilter();
        if (AnimeGroups.Any())
            return;
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

            //TODO IMPORTANT CO JEZELI NIE MAMY MALID TUTAJ...
            var animeFieldSet = await _animeService.GetFieldsAsync(malId.Value, fields:[AnimeField.TITLE, AnimeField.EPISODES]);

            var episode = new DownloadedEpisode(filePath, int.Parse(parsedFile.EpisodeNumber ?? "0"),
                parsedFile.AbsoluteEpisodeNumber,
                animeFieldSet.Title!, malId.Value, parsedFile.Season);

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => AddEpisodeToGroup(episode, animeFieldSet.Title!, animeFieldSet.NumEpisodes ?? 0, malId.Value));

            int current = Interlocked.Increment(ref processed);
            ProcessingProgress = $"Processing files: {current}/{total}";
        });

        IsLoading = false;
        UpdateView();
    }

    private void AddEpisodeToGroup(DownloadedEpisode downloadedEpisode, string animeName, int animeTotalEpisodes, int malId)
    {
        AnimeGroup? existingGroup = AnimeGroups.FirstOrDefault(g => g.Title == animeName);
        
        if (existingGroup != null)
        {
            InsertEpisodeInSortedOrder(existingGroup.Episodes, downloadedEpisode);
        }
        else
        {
            AnimeGroup newGroup = new(downloadedEpisode.AnimeTitle, new ObservableCollection<DownloadedEpisode> { downloadedEpisode }, animeTotalEpisodes, malId, _animeService);
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
        _lastPlayedEpisode = ep;
        _discordService.SetPresenceEpisode(ep.AnimeTitle, ep.EpisodeNumber);

        try
        {
            var process = _videoPlayerService.OpenVideo(ep.FilePath);
            
            if (process != null)
            {
                process.EnableRaisingEvents = true;
                process.Exited += (_, _) => OnVideoPlayerClosed();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error launching video player");
        }
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

    [RelayCommand]
    private void OpenEpisodesFolder()
    {
        SettingsConfig? config = _saveService.GetSettingsConfig();
        string episodesFolder = config?.EpisodesFolder ?? _saveService.DefaultEpisodesFolder;
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start("explorer.exe", episodesFolder.Replace("/", "\\"));
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", episodesFolder);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Process.Start("xdg-open", episodesFolder);
        }
    }

    private void OnVideoPlayerClosed()
    {
        Log.Information("Video player closed!");

        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (_lastPlayedEpisode == null) return;
            if (Avalonia.Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var animeData = await _animeService.GetFieldsAsync(_lastPlayedEpisode.Id, fields: AnimeField.EPISODES);
                ConfirmEpisodeWindow dialog = new() 
                {
                    DataContext = new ConfirmEpisodeViewModel(_lastPlayedEpisode.EpisodeNumber, animeData.NumEpisodes!.Value)
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
        _ = _animeService.SetEpisodesWatchedAsync(ep.Id,  episodeToMark);
    }

    public void Dispose()
    {
        _fileWatcher?.Dispose();
        _debounceTimer?.Dispose();
    }
}