using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Aniki.Services.Anime;
using Aniki.Services.Anime.Providers;
using Aniki.Services.Interfaces;
using Aniki.Services.Parser;
using Aniki.Views;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;

namespace Aniki.ViewModels;

public partial class DownloadedViewModel : ViewModelBase, IDisposable
{
    private DownloadedEpisode? _lastPlayedEpisode;
    private FileSystemWatcher? _fileWatcher;
    private System.Timers.Timer? _debounceTimer;
    private readonly Lock _debounceLock = new();
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private Task _loadTask = Task.CompletedTask;
    private bool _suppressFileWatcher;
    
    private readonly HashSet<string> _pendingProcessPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _pendingRemovePaths = new(StringComparer.OrdinalIgnoreCase);
    
    private static readonly string[] VideoExtensions = [".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv"];
    
    [ObservableProperty] private ObservableCollection<AnimeGroup> _animeGroups = [];
    [ObservableProperty] private ObservableCollection<AnimeGroup> _filteredAnimeGroups = [];
    [ObservableProperty] private bool _isEpisodesViewVisible;
    [ObservableProperty] private bool _isNoEpisodesViewVisible;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _processingProgress = "";
    [ObservableProperty] private string _episodesFolderMessage = "";
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _sortBy = "NameAsc";

    public string SortText => SortBy switch
    {
        "NameDesc"     => "Name (Z-A)",
        "OnDiskDesc"   => "Most on Disk",
        "OnDiskAsc"    => "Least on Disk",
        "ProgressDesc" => "Most Watched",
        "ProgressAsc"  => "Least Watched",
        "TotalDesc"    => "Most Episodes",
        "TotalAsc"     => "Fewest Episodes",
        _              => "Name (A-Z)"
    };

    private readonly IDiscordService _discordService;
    private readonly IAnimeService _animeService;
    private readonly ISaveService _saveService;
    private readonly IAbsoluteEpisodeParser _absoluteEpisodeParser;
    private readonly IAnimeNameParser _animeNameParser;
    private readonly IVideoPlayerService _videoPlayerService;
    private readonly AnilistService _anilistService;

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
        IAbsoluteEpisodeParser absoluteEpisodeParser, IAnimeNameParser animeNameParser,
        IVideoPlayerService videoPlayerService, AnilistService anilistService)
    {
        _discordService         = discordService;
        _animeService           = animeService;
        _saveService            = saveService;
        _absoluteEpisodeParser  = absoluteEpisodeParser;
        _animeNameParser        = animeNameParser;
        _videoPlayerService     = videoPlayerService;
        _anilistService         = anilistService;
        IsEpisodesViewVisible   = false;
        IsNoEpisodesViewVisible = true;
        _loadTask               = RefreshAsync();
        
        SetupFileWatcher();
        
        WeakReferenceMessenger.Default.Register<SettingsChangedMessage>(this, (_, _) =>
        {
            SetupFileWatcher();
            _loadTask = RefreshAsync();
        });
    }

    partial void OnSearchTextChanged(string value) => ApplyFiltersAndSort();

    partial void OnSortByChanged(string value)
    {
        OnPropertyChanged(nameof(SortText));
        ApplyFiltersAndSort();
    }

    [RelayCommand]
    private void SetSort(string sortBy) => SortBy = sortBy;

    [RelayCommand]
    private void ClearSearch() => SearchText = "";

    private void ApplyFiltersAndSort()
    {
        string search = SearchText?.Trim() ?? "";
        IEnumerable<AnimeGroup> groups = AnimeGroups;
        if (!string.IsNullOrEmpty(search))
            groups = groups.Where(g => g.Title.Contains(search, StringComparison.OrdinalIgnoreCase));
        groups = SortBy switch
        {
            "OnDiskDesc"   => groups.OrderByDescending(g => g.OnDiskCount),
            "OnDiskAsc"    => groups.OrderBy(g => g.OnDiskCount),
            "NameDesc"     => groups.OrderByDescending(g => g.HasOnDisk).ThenByDescending(g => g.Title),
            "ProgressDesc" => groups.OrderByDescending(g => g.HasOnDisk).ThenByDescending(g => g.WatchedEpisodes),
            "ProgressAsc"  => groups.OrderByDescending(g => g.HasOnDisk).ThenBy(g => g.WatchedEpisodes),
            "TotalDesc"    => groups.OrderByDescending(g => g.HasOnDisk).ThenByDescending(g => g.MaxEpisodes),
            "TotalAsc"     => groups.OrderByDescending(g => g.HasOnDisk).ThenBy(g => g.MaxEpisodes),
            _              => groups.OrderByDescending(g => g.HasOnDisk).ThenBy(g => g.Title)
        };
        FilteredAnimeGroups = new ObservableCollection<AnimeGroup>(groups);
        UpdateView();
    }

    public override async Task Enter()
    {
        SearchText = "";
        await _loadTask;
        ApplyFiltersAndSort();
    }

    private async Task RefreshAsync()
    {
        await _loadLock.WaitAsync();
        try
        {
            _suppressFileWatcher = true;
            lock (_debounceLock)
            {
                _pendingProcessPaths.Clear();
                _pendingRemovePaths.Clear();
            }

            await LoadDiskCoreAsync();
            await LoadWatchingListAsync();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsLoading = false;
                ApplyFiltersAndSort();
            });
            _ = LoadAiringInfoAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"RefreshAsync failed: {ex}");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsLoading = false;
                ApplyFiltersAndSort();
            });
        }
        finally
        {
            _suppressFileWatcher = false;
            _loadLock.Release();
        }
    }

    private async Task LoadDiskCoreAsync()
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsLoading               = true;
                IsEpisodesViewVisible   = false;
                IsNoEpisodesViewVisible = false;
                AnimeGroups.Clear();
                FilteredAnimeGroups = [];
                ProcessingProgress  = "";
            });
            string episodesFolder = GetEpisodesFolder();
            await Dispatcher.UIThread.InvokeAsync(() =>
                EpisodesFolderMessage = $"Episodes folder is empty - {episodesFolder}");
            if (!Directory.Exists(episodesFolder))
                return;
            List<string> looseFiles = Directory.GetFiles(episodesFolder, "*.*", SearchOption.TopDirectoryOnly)
                                               .Where(IsVideoFile)
                                               .ToList();
            List<(string FolderName, List<string> Files)> animeFolders = [];
            foreach (string dir in Directory.GetDirectories(episodesFolder))
            {
                string folderName = Path.GetFileName(dir);
                if (string.IsNullOrWhiteSpace(folderName))
                    continue;
                List<string> files = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories)
                                              .Where(IsVideoFile)
                                              .ToList();
                if (files.Count > 0)
                    animeFolders.Add((folderName, files));
            }

            int total     = looseFiles.Count + animeFolders.Sum(f => f.Files.Count);
            int processed = 0;
            await Dispatcher.UIThread.InvokeAsync(() =>
                ProcessingProgress = $"Scanning episodes: 0/{total}");
            await Parallel.ForEachAsync(looseFiles, new ParallelOptions { MaxDegreeOfParallelism = 8 },
                async (filePath, ct) =>
                {
                    try
                    {
                        await ProcessLooseFileAsync(filePath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex + " " + filePath);
                    }
                    finally
                    {
                        int current = Interlocked.Increment(ref processed);
                        _ = Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            ProcessingProgress = $"Scanning episodes: {current}/{total}";
                        });
                    }
                });
            await Parallel.ForEachAsync(animeFolders, new ParallelOptions { MaxDegreeOfParallelism = 8 },
                async (folder, ct) =>
                {
                    try
                    {
                        await ProcessAnimeFolderAsync(folder.FolderName, folder.Files, () =>
                        {
                            int current = Interlocked.Increment(ref processed);
                            _ = Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                ProcessingProgress = $"Scanning episodes: {current}/{total}";
                            });
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex + " " + folder.FolderName);
                    }
                });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoadDiskCoreAsync failed: {ex}");
        }
    }

    private async Task ProcessLooseFileAsync(string filePath)
    {
        string      fileName   = Path.GetFileName(filePath);
        ParseResult parsedFile = await _animeNameParser.ParseAnimeFilename(fileName);
        int? animeId = parsedFile.AnimeId ?? await _absoluteEpisodeParser.GetIdForSeason(parsedFile.AnimeName, parsedFile.Season,
            parsedFile.Part, parsedFile.Year, parsedFile.Season);
        if (animeId is null) return;
        AnimeDetails? details = await _animeService.GetFieldsAsync(animeId.Value,
            fields: [AnimeField.Title, AnimeField.Episodes, AnimeField.MyListStatus, AnimeField.MainPicture, AnimeField.Id]);
        if (details == null) return;
        string epNum = parsedFile.EpisodeNumber
                       ?? (details.NumEpisodes is > 1 ? "0" : "1");
        DownloadedEpisode episode = new(
            filePath,
            int.Parse(epNum),
            parsedFile.AbsoluteEpisodeNumber,
            details.Title!,
            animeId.Value,
            parsedFile.Season);
        await Dispatcher.UIThread.InvokeAsync(() => AddEpisodeToGroup(episode, details));
    }

    private async Task ProcessSingleFolderFileAsync(string folderName, string filePath)
    {
        FolderParseResult folderInfo = _animeNameParser.ParseReleaseFolder(folderName);
        int? animeId = await _absoluteEpisodeParser.GetIdForSeason(folderInfo.AnimeName, folderInfo.Season,
            folderInfo.Part, folderInfo.Year, folderInfo.Season);
        if (animeId == null) return;
        AnimeDetails? details = await _animeService.GetFieldsAsync(animeId.Value,
            fields: [AnimeField.Title, AnimeField.Episodes, AnimeField.MyListStatus, AnimeField.MainPicture, AnimeField.Id]);
        if (details == null) return;
        string fileName = Path.GetFileName(filePath);
        EpisodeInfo? episodeInfo =
            _animeNameParser.ParseEpisodeFromFilename(fileName, folderInfo.Season, folderInfo.Part);
        if (episodeInfo == null) return;
        DownloadedEpisode episode = new(
            filePath,
            episodeInfo.EpisodeNumber,
            episodeInfo.EpisodeNumber,
            details.Title!,
            animeId.Value,
            episodeInfo.Season);
        await Dispatcher.UIThread.InvokeAsync(() => AddEpisodeToGroup(episode, details));
    }

    private async Task ProcessFileAsync(string filePath)
    {
        if (!File.Exists(filePath) || !IsVideoFile(filePath)) return;
        string fullPath       = Path.GetFullPath(filePath);
        string episodesFolder = GetEpisodesFolder();
        await Dispatcher.UIThread.InvokeAsync(() => RemoveEpisodeByPath(fullPath));
        if (IsLooseFile(episodesFolder, fullPath))
        {
            await ProcessLooseFileAsync(fullPath);
            return;
        }

        string folderName = GetAnimeFolderName(episodesFolder, fullPath);
        await ProcessSingleFolderFileAsync(folderName, fullPath);
    }

    private async Task ApplyPendingFileChangesAsync()
    {
        await _loadLock.WaitAsync();
        try
        {
            List<string> toRemove;
            List<string> toProcess;
            lock (_debounceLock)
            {
                toRemove  = [.._pendingRemovePaths];
                toProcess = [.._pendingProcessPaths];
                _pendingRemovePaths.Clear();
                _pendingProcessPaths.Clear();
            }

            if (toRemove.Count == 0 && toProcess.Count == 0) return;
            foreach (string path in toRemove)
                await Dispatcher.UIThread.InvokeAsync(() => RemoveEpisodeByPath(path));
            foreach (string path in toProcess)
            {
                try
                {
                    await ProcessFileAsync(path);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex + " " + path);
                }
            }

            await Dispatcher.UIThread.InvokeAsync(ApplyFiltersAndSort);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ApplyPendingFileChangesAsync failed: {ex}");
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private async Task ProcessAnimeFolderAsync(string folderName, List<string> filePaths, Action onFileProcessed)
    {
        FolderParseResult folderInfo = _animeNameParser.ParseReleaseFolder(folderName);
        int? animeId = await _absoluteEpisodeParser.GetIdForSeason(folderInfo.AnimeName, folderInfo.Season,
            folderInfo.Part, folderInfo.Year, folderInfo.Season);
        if (animeId == null)
        {
            for (int i = 0; i < filePaths.Count; i++)
                onFileProcessed();
            return;
        }

        AnimeDetails? details = await _animeService.GetFieldsAsync(animeId.Value,
            fields: [AnimeField.Title, AnimeField.Episodes, AnimeField.MyListStatus, AnimeField.MainPicture, AnimeField.Id]);
        if (details == null)
        {
            for (int i = 0; i < filePaths.Count; i++)
                onFileProcessed();
            return;
        }

        foreach (string filePath in filePaths)
        {
            try
            {
                string fileName = Path.GetFileName(filePath);
                EpisodeInfo? episodeInfo =
                    _animeNameParser.ParseEpisodeFromFilename(fileName, folderInfo.Season, folderInfo.Part);
                if (episodeInfo == null)
                    continue;
                DownloadedEpisode episode = new(
                    filePath,
                    episodeInfo.EpisodeNumber,
                    episodeInfo.EpisodeNumber,
                    details.Title!,
                    animeId.Value,
                    episodeInfo.Season);
                await Dispatcher.UIThread.InvokeAsync(() => AddEpisodeToGroup(episode, details));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex + " " + filePath);
            }
            finally
            {
                onFileProcessed();
            }
        }
    }

    private async Task LoadWatchingListAsync()
    {
        if (!AnimeService.IsLoggedIn) return;
        List<AnimeDetails> watchingList = await _animeService.GetUserAnimeListAsync(AnimeStatus.Watching);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (AnimeDetails anime in watchingList)
            {
                if (AnimeGroups.Any(g => g.MalId == anime.Id)) continue;
                AnimeGroup group = new(
                    anime.Title ?? "",
                    anime.MainPicture?.Large,
                    [],
                    anime.NumEpisodes ?? 0,
                    anime.UserStatus?.EpisodesWatched ?? 0,
                    anime.Id,
                    _animeService);
                InsertGroupAlphabetically(group);
            }
        });
    }

    private async Task LoadAiringInfoAsync()
    {
        List<(int MalId, AnimeGroup Group)> snapshot =
            await Dispatcher.UIThread.InvokeAsync(() => AnimeGroups.Select(g => (g.MalId, g)).ToList());
        foreach ((int malId, AnimeGroup group) in snapshot)
        {
            try
            {
                int released = await _anilistService.GetReleasedEpisodeCountAsync(malId);
                await Dispatcher.UIThread.InvokeAsync(() => group.ReleasedEpisodes = released);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AniList airing info failed for {malId}: {ex.Message}");
            }
        }
    }

    private string GetEpisodesFolder()
    {
        SettingsConfig? config = _saveService.GetSettingsConfig();
        return config?.EpisodesFolder ?? _saveService.DefaultEpisodesFolder;
    }

    private static bool IsVideoFile(string path) =>
        VideoExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    private static bool IsLooseFile(string episodesFolder, string filePath) =>
        string.Equals(
            Path.GetFullPath(Path.GetDirectoryName(filePath)!),
            Path.GetFullPath(episodesFolder),
            StringComparison.OrdinalIgnoreCase);

    private static string GetAnimeFolderName(string episodesFolder, string filePath)
    {
        string relative = Path.GetRelativePath(Path.GetFullPath(episodesFolder), Path.GetFullPath(filePath));
        return relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
    }

    private void RemoveEpisodeByPath(string filePath)
    {
        string normalized = Path.GetFullPath(filePath);
        foreach (AnimeGroup group in AnimeGroups)
        {
            DownloadedEpisode? ep = group.Episodes.FirstOrDefault(e =>
                string.Equals(Path.GetFullPath(e.FilePath), normalized, StringComparison.OrdinalIgnoreCase));
            if (ep == null) continue;
            group.Episodes.Remove(ep);
            return;
        }
    }

    private void QueueFileRemoval(string fullPath)
    {
        lock (_debounceLock)
        {
            _pendingRemovePaths.Add(fullPath);
            _pendingProcessPaths.Remove(fullPath);
        }

        RestartDebounceTimer();
    }

    private void QueueFileProcessing(string fullPath)
    {
        if (!IsVideoFile(fullPath)) return;
        lock (_debounceLock)
        {
            _pendingProcessPaths.Add(fullPath);
            _pendingRemovePaths.Remove(fullPath);
        }

        RestartDebounceTimer();
    }

    private void RestartDebounceTimer()
    {
        lock (_debounceLock)
        {
            _debounceTimer?.Stop();
            _debounceTimer?.Start();
        }
    }

    private void AddEpisodeToGroup(DownloadedEpisode episode, AnimeDetails details)
    {
        string      normalizedPath = Path.GetFullPath(episode.FilePath);
        AnimeGroup? existing       = AnimeGroups.FirstOrDefault(g => g.MalId == details.Id);
        if (existing != null)
        {
            DownloadedEpisode? duplicate = existing.Episodes.FirstOrDefault(e =>
                string.Equals(Path.GetFullPath(e.FilePath), normalizedPath, StringComparison.OrdinalIgnoreCase));
            if (duplicate != null)
                existing.Episodes.Remove(duplicate);
            InsertEpisodeInSortedOrder(existing.Episodes, episode);
        }
        else
        {
            AnimeGroup newGroup = new(
                details.Title ?? "",
                details.MainPicture?.Large,
                [episode],
                details.NumEpisodes ?? 0,
                details.UserStatus?.EpisodesWatched ?? 0,
                details.Id,
                _animeService);
            InsertGroupAlphabetically(newGroup);
        }
    }

    private void InsertGroupAlphabetically(AnimeGroup newGroup)
    {
        int insertIndex = AnimeGroups.Count;
        for (int i = 0; i < AnimeGroups.Count; i++)
        {
            if (string.Compare(newGroup.Title, AnimeGroups[i].Title, StringComparison.OrdinalIgnoreCase) < 0)
            {
                insertIndex = i;
                break;
            }
        }

        AnimeGroups.Insert(insertIndex, newGroup);
    }

    private static void InsertEpisodeInSortedOrder(ObservableCollection<DownloadedEpisode> episodes,
        DownloadedEpisode newEp)
    {
        int insertIndex = 0;
        for (int i = 0; i < episodes.Count; i++)
        {
            DownloadedEpisode existing = episodes[i];
            if (newEp.Season < existing.Season)
            {
                insertIndex = i;
                break;
            }

            if (newEp.Season == existing.Season && newEp.EpisodeNumber < existing.EpisodeNumber)
            {
                insertIndex = i;
                break;
            }

            insertIndex = i + 1;
        }

        episodes.Insert(insertIndex, newEp);
    }

    private void UpdateView()
    {
        bool hasContent = AnimeGroups.Count > 0;
        IsEpisodesViewVisible   = hasContent;
        IsNoEpisodesViewVisible = !hasContent;
    }

    private void SetupFileWatcher()
    {
        _fileWatcher?.Dispose();
        lock (_debounceLock)
        {
            _debounceTimer?.Dispose();
        }

        string episodesFolder = GetEpisodesFolder();
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

        lock (_debounceLock)
        {
            _debounceTimer = new System.Timers.Timer(500);
        }

        _debounceTimer.AutoReset = false;
        _debounceTimer.Elapsed += async (_, _) =>
        {
            if (_suppressFileWatcher) return;
            try
            {
                await ApplyPendingFileChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        };
        _fileWatcher = new FileSystemWatcher(episodesFolder)
        {
            NotifyFilter          = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            Filter                = "*.*",
            IncludeSubdirectories = true,
            EnableRaisingEvents   = true
        };
        _fileWatcher.Created += OnFileChanged;
        _fileWatcher.Deleted += OnFileChanged;
        _fileWatcher.Renamed += OnFileRenamed;
        _fileWatcher.Changed += OnFileChanged;
        _fileWatcher.Error   += OnWatcherError;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (_suppressFileWatcher) return;
        if (e.ChangeType == WatcherChangeTypes.Deleted)
        {
            QueueFileRemoval(Path.GetFullPath(e.FullPath));
            return;
        }

        QueueFileProcessing(Path.GetFullPath(e.FullPath));
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        if (_suppressFileWatcher) return;
        QueueFileRemoval(Path.GetFullPath(e.OldFullPath));
        if (IsVideoFile(e.FullPath))
            QueueFileProcessing(Path.GetFullPath(e.FullPath));
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        Console.WriteLine(e.GetException());
        Task.Delay(2000).ContinueWith(_ => SetupFileWatcher());
    }

    [RelayCommand]
    private async Task DownloadNextEpisode(AnimeGroup group)
    {
        if (group.NextEpisodeToDownload is not { } epNum) return;
        MainViewModel mainVm = DependencyInjection.Instance.ServiceProvider!.GetRequiredService<MainViewModel>();
        AnimeDetailsViewModel detailsVm =
            DependencyInjection.Instance.ServiceProvider!.GetRequiredService<AnimeDetailsViewModel>();
        await mainVm.GoToAnime(group.MalId);
        detailsVm.SelectedTabIndex                          = 1;
        detailsVm.TorrentSearchViewModel.TorrentSearchTerms = $"{group.Title} {epNum:D2}";
        _                                                   = detailsVm.TorrentSearchViewModel.SearchTorrents();
    }

    [RelayCommand]
    private async Task OpenAnimeDetails(int malId)
    {
        MainViewModel vm = DependencyInjection.Instance.ServiceProvider!.GetRequiredService<MainViewModel>();
        await vm.GoToAnime(malId);
    }

    [RelayCommand]
    public void LaunchEpisode(DownloadedEpisode ep)
    {
        _lastPlayedEpisode = ep;
        _discordService.SetPresenceEpisode(ep.AnimeTitle, ep.EpisodeNumber);
        try
        {
            Process? process = _videoPlayerService.OpenVideo(ep.FilePath);
            if (process != null)
            {
                process.EnableRaisingEvents =  true;
                process.Exited              += (_, _) => OnVideoPlayerClosed();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }

    [RelayCommand]
    private void DeleteEpisode(DownloadedEpisode ep)
    {
        File.Delete(ep.FilePath);
        AnimeGroup? group = AnimeGroups.FirstOrDefault(g => g.Episodes.Contains(ep));
        group?.Episodes.Remove(ep);
        ApplyFiltersAndSort();
    }

    [RelayCommand]
    private void OpenEpisodesFolder()
    {
        string episodesFolder = GetEpisodesFolder();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Process.Start("explorer.exe", episodesFolder.Replace("/", "\\"));
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Process.Start("open", episodesFolder);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Process.Start("xdg-open", episodesFolder);
    }

    private void OnVideoPlayerClosed()
    {
        if (!AnimeService.IsLoggedIn) return;
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (_lastPlayedEpisode == null) return;
            if (Avalonia.Application.Current!.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
                desktop) return;
            AnimeDetails? animeData =
                await _animeService.GetFieldsAsync(_lastPlayedEpisode.Id, fields: AnimeField.Episodes);
            if (animeData == null) return;
            ConfirmEpisodeWindow dialog = new()
            {
                DataContext =
                    new ConfirmEpisodeViewModel(_lastPlayedEpisode.EpisodeNumber, animeData.NumEpisodes!.Value)
            };
            bool result = await dialog.ShowDialog<bool>(desktop.MainWindow!);
            if (result)
                _ = _animeService.SetEpisodesWatchedAsync(_lastPlayedEpisode.Id, _lastPlayedEpisode.EpisodeNumber);
        });
        _discordService.Reset();
    }

    public void Dispose()
    {
        WeakReferenceMessenger.Default.Unregister<SettingsChangedMessage>(this);
        _fileWatcher?.Dispose();
        _debounceTimer?.Dispose();
        _loadLock.Dispose();
    }
}
