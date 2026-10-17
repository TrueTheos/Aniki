using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Aniki.Services.Anime;
using Aniki.Services.Anime.Providers;
using Aniki.Services.Auth;
using Aniki.Services.Interfaces;
using Aniki.Services.Parser;
using Aniki.Views;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;

namespace Aniki.ViewModels;

// I really don't want to touch this. This is such a mess
// Some day I will need to answer for my actions, 
// but not today.
internal sealed partial class DownloadedViewModel : ViewModelBase, IDisposable
{
    private DownloadedEpisode? _lastPlayedEpisode;
    
    private readonly HashSet<string> _pendingProcessPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _pendingRemovePaths = new(StringComparer.OrdinalIgnoreCase);
    

    [ObservableProperty] public partial ObservableCollection<AnimeGroup> AnimeGroups { get; set; } = [];
    [ObservableProperty] public partial ObservableCollection<AnimeGroup> FilteredAnimeGroups { get; set; } = [];
    [ObservableProperty] public partial bool IsEpisodesViewVisible { get; set; }
    [ObservableProperty] public partial bool IsNoEpisodesViewVisible { get; set; }
    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial string ProcessingProgress { get; set; } = "";
    [ObservableProperty] public partial string EpisodesFolderMessage { get; set; } = "";
    [ObservableProperty] public partial string SearchText { get; set; } = "";
    [ObservableProperty] public partial string SortBy { get; set; } = "NameAsc";

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
    private readonly IAnimeNameParser _animeNameParser;
    private readonly IVideoPlayerService _videoPlayerService;
    private readonly AnilistService _anilistService;
    private readonly ITorrentDownloadService _torrentDownloads;

    public ObservableCollection<TorrentDownload> ActiveDownloads => _torrentDownloads.Downloads;

    private FileSystemWatcher? _fileWatcher;
    private System.Timers.Timer? _debounceTimer;
    private readonly Lock _debounceLock = new();
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly SemaphoreSlim _metadataLock = new(1, 1);
    private Task _loadTask;
    private bool _suppressFileWatcher;
    private string? _watchedFolder;

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
        IAnimeNameParser animeNameParser, IVideoPlayerService videoPlayerService, AnilistService anilistService,
        ITorrentDownloadService torrentDownloads)
    {
        _discordService        = discordService;
        _animeService          = animeService;
        _saveService           = saveService;
        _animeNameParser       = animeNameParser;
        _videoPlayerService    = videoPlayerService;
        _anilistService        = anilistService;
        _torrentDownloads      = torrentDownloads;
        
        IsEpisodesViewVisible   = false;
        IsNoEpisodesViewVisible = true;
        _loadTask               = RefreshAsync();
        
        SetupFileWatcher();
        
        WeakReferenceMessenger.Default.Register<SettingsChangedMessage>(this, (_, _) =>
        {
            if (GetEpisodesFolder() == _watchedFolder) return;
            SetupFileWatcher();
            _loadTask = RefreshAsync();
        });
    }
    
    public override async Task Enter()
    {
        SearchText = "";
        if (!_loadTask.IsCompleted)
            await _loadTask.ConfigureAwait(true);
        else
            await LoadUserLibraryData().ConfigureAwait(true);
        ApplyFiltersAndSort();
    }

    partial void OnSearchTextChanged(string value) => ApplyFiltersAndSort();

    partial void OnSortByChanged(string value)
    {
        OnPropertyChanged(nameof(SortText));
        ApplyFiltersAndSort();
    }

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

    /// <summary>
    /// Clears UI and scans disk for episodes
    /// </summary>
    private async Task RefreshAsync()
    {
        await _loadLock.WaitAsync().ConfigureAwait(true);
        try
        {
            _suppressFileWatcher = true;
            lock (_debounceLock)
            {
                _pendingProcessPaths.Clear();
                _pendingRemovePaths.Clear();
            }

            await LoadEpisodesFromDiskAsync().ConfigureAwait(true);
            await LoadUserLibraryData().ConfigureAwait(true);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsLoading = false;
                ApplyFiltersAndSort();
            });
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

    private async Task LoadEpisodesFromDiskAsync()
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsLoading               = true;
                IsEpisodesViewVisible   = false;
                IsNoEpisodesViewVisible = false;
                foreach (AnimeGroup group in AnimeGroups)
                    group.Unsubscribe();
                AnimeGroups.Clear();
                FilteredAnimeGroups = [];
                ProcessingProgress  = "";
            });
            
            string episodesFolder = GetEpisodesFolder();
            await Dispatcher.UIThread.InvokeAsync(() => EpisodesFolderMessage = $"Episodes folder is empty - {episodesFolder}");
            if (!Directory.Exists(episodesFolder))
                return;

            var looseFiles = Directory.GetFiles(episodesFolder, "*.*", SearchOption.TopDirectoryOnly).Where(IsVideoFile).ToList();
            var animeFolders = CollectAnimeFolders(episodesFolder);
            
            int total = looseFiles.Count + animeFolders.Sum(f => f.Files.Count);
            int processed = 0;
            
            await Dispatcher.UIThread.InvokeAsync(() =>
                ProcessingProgress = $"Scanning episodes: 0/{total}");
            
            await Parallel.ForEachAsync(looseFiles, new ParallelOptions { MaxDegreeOfParallelism = 8 },
                async (filePath, ct) =>
                {
                    try
                    {
                        await ProcessLooseFileAsync(filePath).ConfigureAwait(true);
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
                }).ConfigureAwait(true);
            
            await Parallel.ForEachAsync(animeFolders, new ParallelOptions { MaxDegreeOfParallelism = 8 },
                async (folder, ct) =>
                {
                    try
                    {
                        await ProcessAnimeFolderAsync(episodesFolder, folder.FolderName, folder.Files).ConfigureAwait(true);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex + " " + folder.FolderName);
                    }
                    finally
                    {
                        int current = Interlocked.Increment(ref processed);
                        _ = Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            ProcessingProgress = $"Scanning episodes: {current}/{total}";
                        });
                    }
                }).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoadDiskCoreAsync failed: {ex}");
        }
    }

    private static List<(string FolderName, List<string> Files)> CollectAnimeFolders(string path)
    {
        List<(string FolderName, List<string> Files)> result = [];
        foreach (string dir in Directory.GetDirectories(path))
        {
            string folderName = Path.GetFileName(dir);
            if (string.IsNullOrWhiteSpace(folderName))
                continue;
            var files = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories).Where(IsVideoFile).ToList();
            if (files.Count > 0)
                result.Add((folderName, files));
        }

        return result;
    }

    private async Task ProcessLooseFileAsync(string filePath)
    {
        string fileName = Path.GetFileName(filePath);
        ParseResult parsed = await _animeNameParser.ParseFile(fileName).ConfigureAwait(true);
        if (parsed.AnimeId is not { } animeId) return;

        AnimeDetails? details = await _animeService.GetFieldsAsync(animeId,
            fields: [AnimeField.Title, AnimeField.Episodes, AnimeField.MyListStatus, AnimeField.MainPicture, AnimeField.Id]).ConfigureAwait(true);
        if (details == null) return;

        int episodeNumber = parsed.EpisodeNumber ?? (details.NumEpisodes is > 1 ? 0 : 1);
        DownloadedEpisode episode = new(
            filePath,
            episodeNumber,
            details.Title!,
            animeId,
            parsed.Season);
        await Dispatcher.UIThread.InvokeAsync(() => AddEpisodeToGroup(episode, details));
    }

    private async Task ProcessFileAsync(string filePath)
    {
        if (!File.Exists(filePath) || !IsVideoFile(filePath)) return;
        string fullPath = Path.GetFullPath(filePath);
        string episodesFolder = GetEpisodesFolder();
        await Dispatcher.UIThread.InvokeAsync(() => RemoveEpisodeByPath(fullPath));
        if (IsLooseFile(episodesFolder, fullPath))
        {
            await ProcessLooseFileAsync(fullPath).ConfigureAwait(true);
            return;
        }

        string topFolder = Path.GetRelativePath(episodesFolder, fullPath).Split(Path.DirectorySeparatorChar)[0];
        await ProcessAnimeFolderAsync(episodesFolder, topFolder, [fullPath]).ConfigureAwait(true);
    }

    private async Task ApplyPendingFileChangesAsync()
    {
        await _loadLock.WaitAsync().ConfigureAwait(true);
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
                    await ProcessFileAsync(path).ConfigureAwait(true);
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

    private async Task ProcessAnimeFolderAsync(string episodesFolder, string folderName, List<string> filePaths)
    {
        FolderParseResult folder = await _animeNameParser.ParseFolder(folderName).ConfigureAwait(true);
        string folderPath = Path.Combine(episodesFolder, folderName);

        foreach (string filePath in filePaths)
        {
            try
            {
                string fileName = Path.GetFileName(filePath);
                ParseResult parsed = await _animeNameParser.ParseFile(fileName).ConfigureAwait(true);

                int? animeId;
                int? episodeNumber;
                int season;
                if (parsed.AnimeId != null && parsed.EpisodeNumber != null && NameKey(parsed.AnimeName) == NameKey(folder.AnimeName))
                {
                    (animeId, episodeNumber, season) = (parsed.AnimeId, parsed.EpisodeNumber, parsed.Season);
                }
                else
                {
                    FolderParseResult target = folder;
                    if (SubfolderSeason(folderPath, filePath) is { } subSeason && subSeason != folder.Season)
                        target = await _animeNameParser.ParseFolder($"{folder.AnimeName} Season {subSeason}").ConfigureAwait(true);
                    (animeId, episodeNumber, season) = (target.AnimeId, _animeNameParser.ParseEpisode(fileName), target.Season);
                }

                if (animeId is not { } id || episodeNumber is not { } episodeNum)
                    continue;

                AnimeDetails? details = await _animeService.GetFieldsAsync(id,
                    fields: [AnimeField.Title, AnimeField.Episodes, AnimeField.MyListStatus, AnimeField.MainPicture, AnimeField.Id]).ConfigureAwait(true);
                if (details == null)
                    continue;

                DownloadedEpisode episode = new(filePath, episodeNum, details.Title!, id, season);
                await Dispatcher.UIThread.InvokeAsync(() => AddEpisodeToGroup(episode, details));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex + " " + filePath);
            }
        }
    }
    
    private static string NameKey(string name) =>
        new(name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static int? SubfolderSeason(string folderPath, string filePath)
    {
        string relative = Path.GetRelativePath(folderPath, Path.GetDirectoryName(filePath)!);
        if (relative == ".") return null;
        return relative.Split(Path.DirectorySeparatorChar)
            .Select(dir => FilenameParser.Parse(dir, extractEpisode: false).Season)
            .LastOrDefault(s => s != null);
    }

    private async Task LoadUserLibraryData()
    {
        if (!AnimeService.IsLoggedIn) return;

        await _metadataLock.WaitAsync().ConfigureAwait(true);
        try
        {
            List<AnimeDetails> watchingList = [];
            try
            {
                watchingList = await LoadWatchingListAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LoadWatchingListAsync failed: {ex}");
            }

            await LoadAiringInfoAsync(watchingList).ConfigureAwait(true);
        }
        finally
        {
            _metadataLock.Release();
        }
    }

    private async Task<List<AnimeDetails>> LoadWatchingListAsync()
    {
        var watchingList = await _animeService.GetUserAnimeListAsync(AnimeStatus.Watching).ConfigureAwait(true);
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
        return watchingList;
    }

    private async Task LoadAiringInfoAsync(List<AnimeDetails> watchingList)
    {
        Dictionary<int, AnimeGroup> groupsById =
            await Dispatcher.UIThread.InvokeAsync(() => AnimeGroups.ToDictionary(g => g.MalId));

        List<(int Id, AnimeGroup Group)> needAnilist = [];
        List<(AnimeGroup Group, int Released)> localUpdates = [];

        foreach (AnimeDetails anime in watchingList)
        {
            if (!groupsById.TryGetValue(anime.Id, out AnimeGroup? group)) continue;

            string? status = anime.Status;
            int numEpisodes = anime.NumEpisodes ?? group.MaxEpisodes;

            if (IsFinishedAiring(status))
                localUpdates.Add((group, numEpisodes));
            else if (IsCurrentlyAiring(status))
                needAnilist.Add((anime.Id, group));
            else
                localUpdates.Add((group, 0));
        }

        if (localUpdates.Count > 0)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach ((AnimeGroup group, int released) in localUpdates)
                    group.ReleasedEpisodes = released;
            });
        }

        if (needAnilist.Count == 0) return;

        // MAL has no aired-episode field; Anilist is only used for currently-airing titles.
        try
        {
            bool useMalIds = AnimeService.CurrentProviderType == ILoginProvider.ProviderType.Mal;
            var counts = await _anilistService
                .GetReleasedEpisodeCountsAsync(needAnilist.Select(x => x.Id).ToList(), useMalIds)
                .ConfigureAwait(true);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach ((int id, AnimeGroup group) in needAnilist)
                {
                    if (counts.TryGetValue(id, out int released))
                        group.ReleasedEpisodes = released;
                    else if (group.MaxEpisodes > 0)
                        group.ReleasedEpisodes = group.MaxEpisodes;
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AniList airing batch failed: {ex.Message}");
        }
    }

    private static bool IsCurrentlyAiring(string? status) =>
        status is "currently_airing" or "RELEASING";

    private static bool IsFinishedAiring(string? status) =>
        status is "finished_airing" or "FINISHED";

    private string GetEpisodesFolder() => _saveService.EpisodesFolder;

    private static bool IsVideoFile(string path) =>
        FilenameParser.VideoExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    private static bool IsLooseFile(string episodesFolder, string filePath) =>
        string.Equals(
            Path.GetFullPath(Path.GetDirectoryName(filePath)!),
            Path.GetFullPath(episodesFolder),
            StringComparison.OrdinalIgnoreCase);

    private void RemoveEpisodeByPath(string filePath)
    {
        string normalized = Path.GetFullPath(filePath);
        string folderPrefix = normalized.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (AnimeGroup group in AnimeGroups)
        {
            foreach (DownloadedEpisode ep in group.Episodes.ToList())
            {
                string epPath = Path.GetFullPath(ep.FilePath);
                if (string.Equals(epPath, normalized, StringComparison.OrdinalIgnoreCase) ||
                    epPath.StartsWith(folderPrefix, StringComparison.OrdinalIgnoreCase))
                    group.Episodes.Remove(ep);
            }
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
            if (newEp.Season < existing.Season || newEp.Season == existing.Season && newEp.EpisodeNumber < existing.EpisodeNumber)
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

    #region File watcher
    
    private void SetupFileWatcher()
    {
        _fileWatcher?.Dispose();
        lock (_debounceLock)
        {
            _debounceTimer?.Dispose();
        }

        string episodesFolder = GetEpisodesFolder();
        _watchedFolder = episodesFolder;
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
                await ApplyPendingFileChangesAsync().ConfigureAwait(true);
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

        QueueFileOrFolderProcessing(Path.GetFullPath(e.FullPath), e.ChangeType == WatcherChangeTypes.Created);
    }

    private void QueueFileOrFolderProcessing(string fullPath, bool includeFolders)
    {
        if (includeFolders && Directory.Exists(fullPath))
        {
            foreach (string file in Directory.EnumerateFiles(fullPath, "*.*", SearchOption.AllDirectories))
                QueueFileProcessing(Path.GetFullPath(file));
            return;
        }

        QueueFileProcessing(fullPath);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        if (_suppressFileWatcher) return;
        QueueFileRemoval(Path.GetFullPath(e.OldFullPath));
        QueueFileOrFolderProcessing(Path.GetFullPath(e.FullPath), includeFolders: true);
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        Console.WriteLine(e.GetException());
        Task.Delay(2000).ContinueWith(_ => SetupFileWatcher(), TaskScheduler.Default);
    }
    
    #endregion

    private void OnVideoPlayerClosed()
    {
        if (!AnimeService.IsLoggedIn) return;
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (_lastPlayedEpisode == null) return;
            if (Avalonia.Application.Current!.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
                desktop) return;
            AnimeDetails? animeData =
                await _animeService.GetFieldsAsync(_lastPlayedEpisode.Id, fields: [AnimeField.Episodes, AnimeField.MyListStatus]).ConfigureAwait(true);
            if (animeData == null) return;
            ConfirmEpisodeViewModel confirm = new(_lastPlayedEpisode.EpisodeNumber, animeData.NumEpisodes!.Value);
            ConfirmEpisodeWindow dialog = new() { DataContext = confirm };
            bool result = await dialog.ShowDialog<bool>(desktop.MainWindow!).ConfigureAwait(true);
            if (result && confirm.EpisodeNumber > (animeData.UserStatus?.EpisodesWatched ?? 0))
                _ = _animeService.SetEpisodesWatchedAsync(_lastPlayedEpisode.Id, confirm.EpisodeNumber);
        });
        _discordService.Reset();
    }
    
    [RelayCommand]
    private void DeleteEpisode(DownloadedEpisode ep)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(ep.FilePath,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            else
                File.Delete(ep.FilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _ = ToastService.Show($"Could not delete: {ex.Message}");
            return;
        }

        AnimeGroup? group = AnimeGroups.FirstOrDefault(g => g.Episodes.Contains(ep));
        group?.Episodes.Remove(ep);
        ApplyFiltersAndSort();
    }

    [RelayCommand]
    private Task CancelDownload(TorrentDownload download) => _torrentDownloads.CancelAsync(download);

    [RelayCommand]
    private void OpenEpisodesFolder()
    {
        string episodesFolder = GetEpisodesFolder();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Process.Start("explorer.exe", episodesFolder.Replace("/", "\\", StringComparison.InvariantCulture));
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Process.Start("open", episodesFolder);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Process.Start("xdg-open", episodesFolder);
    }
    
    [RelayCommand]
    private void SetSort(string sortBy) => SortBy = sortBy;

    [RelayCommand]
    private void ClearSearch() => SearchText = "";
    
    [RelayCommand]
    private static async Task DownloadNextEpisode(AnimeGroup group)
    {
        if (group.NextEpisodeToDownload is not { } epNum) return;
        MainViewModel mainVm = DependencyInjection.Instance.ServiceProvider!.GetRequiredService<MainViewModel>();
        await mainVm.GoToAnime(group.MalId).ConfigureAwait(true);
        
        AnimeDetailsViewModel animeDetailsViewModel = DependencyInjection.Instance.ServiceProvider!.GetRequiredService<AnimeDetailsViewModel>()!;
        
        animeDetailsViewModel.SelectedTabIndex = 1;
        animeDetailsViewModel.TorrentSearchViewModel.TorrentSearchTerms = $"{group.Title} {epNum:D2}";
        _ = animeDetailsViewModel.TorrentSearchViewModel.SearchTorrents();
    }

    [RelayCommand]
    private static async Task OpenAnimeDetails(int malId)
    {
        MainViewModel vm = DependencyInjection.Instance.ServiceProvider!.GetRequiredService<MainViewModel>();
        await vm.GoToAnime(malId).ConfigureAwait(true);
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

    public void Dispose()
    {
        WeakReferenceMessenger.Default.Unregister<SettingsChangedMessage>(this);
        _fileWatcher?.Dispose();
        _debounceTimer?.Dispose();
        _loadLock.Dispose();
        _metadataLock.Dispose();
    }
}
