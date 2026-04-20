using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Aniki.Services.Anime;
using Aniki.Services.Anime.Providers;
using Aniki.Services.Interfaces;
using Aniki.Views;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Aniki.ViewModels;

public partial class DownloadedViewModel : ViewModelBase, IDisposable
{
    private DownloadedEpisode? _lastPlayedEpisode;
    private FileSystemWatcher? _fileWatcher;
    private System.Timers.Timer? _debounceTimer;
    private readonly Lock _debounceLock = new();

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
        _discordService = discordService;
        _animeService = animeService;
        _saveService = saveService;
        _absoluteEpisodeParser = absoluteEpisodeParser;
        _animeNameParser = animeNameParser;
        _videoPlayerService = videoPlayerService;
        _anilistService = anilistService;

        IsEpisodesViewVisible = false;
        IsNoEpisodesViewVisible = true;

        _ = LoadDiskAsync();
        SetupFileWatcher();

        WeakReferenceMessenger.Default.Register<SettingsChangedMessage>(this, (r, m) =>
        {
            SetupFileWatcher();
            _ = LoadDiskAsync();
        });
    }

    partial void OnSearchTextChanged(string value) => ApplyFiltersAndSort();

    partial void OnSortByChanged(string value)
    {
        OnPropertyChanged(nameof(SortText));
        ApplyFiltersAndSort();
    }

    [RelayCommand] private void SetSort(string sortBy) => SortBy = sortBy;
    [RelayCommand] private void ClearSearch() => SearchText = "";

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
        await LoadWatchingListAsync();
        ApplyFiltersAndSort();
    }

    private async Task LoadDiskAsync()
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsLoading = true;
                AnimeGroups.Clear();
                FilteredAnimeGroups = [];
                ProcessingProgress = "";
            });

            SettingsConfig? config = _saveService.GetSettingsConfig();
            string episodesFolder = config?.EpisodesFolder ?? _saveService.DefaultEpisodesFolder;

            await Dispatcher.UIThread.InvokeAsync(() =>
                EpisodesFolderMessage = $"Episodes folder is empty - {episodesFolder}");

            if (!Directory.Exists(episodesFolder))
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    IsLoading = false;
                    ApplyFiltersAndSort();
                });
                return;
            }

            string[] videoExtensions = [".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv"];
            List<string> filePaths = Directory.GetFiles(episodesFolder, "*.*", SearchOption.AllDirectories)
                .Where(f => videoExtensions.Contains(Path.GetExtension(f).ToLower()))
                .ToList();

            int total = filePaths.Count;
            int processed = 0;

            await Dispatcher.UIThread.InvokeAsync(() =>
                ProcessingProgress = $"Scanning episodes: 0/{total}");

            await Parallel.ForEachAsync(filePaths, new ParallelOptions { MaxDegreeOfParallelism = 8 },
                async (filePath, _) =>
                {
                    try
                    {
                        string fileName = Path.GetFileName(filePath);
                        ParseResult parsedFile = await _animeNameParser.ParseAnimeFilename(fileName);

                        int? animeId = await _absoluteEpisodeParser.GetIdForSeason(parsedFile.AnimeName, parsedFile.Season);
                        if (animeId == null) return;

                        AnimeDetails? details = await _animeService.GetFieldsAsync(animeId.Value,
                            fields: [AnimeField.Title, AnimeField.Episodes, AnimeField.MyListStatus, AnimeField.MainPicture]);

                        if (details != null)
                        {
                            string epNum = parsedFile.EpisodeNumber
                                ?? (details.NumEpisodes is > 1 ? "0" : "1");

                            DownloadedEpisode episode = new(
                                filePath,
                                int.Parse(epNum),
                                parsedFile.AbsoluteEpisodeNumber,
                                details.Title!,
                                animeId.Value,
                                parsedFile.Season);

                            await Dispatcher.UIThread.InvokeAsync(() =>
                                AddEpisodeToGroup(episode, details));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex + " " + filePath);
                    }
                    finally
                    {
                        int current = Interlocked.Increment(ref processed);
                        ProcessingProgress = $"Scanning episodes: {current}/{total}";
                    }
                });

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsLoading = false;
                ApplyFiltersAndSort();
            });

            _ = LoadAiringInfoAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoadDiskAsync failed: {ex}");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsLoading = false;
                ApplyFiltersAndSort();
            });
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

            ApplyFiltersAndSort();
        });

        _ = LoadAiringInfoAsync();
    }

    private async Task LoadAiringInfoAsync()
    {
        List<(int MalId, AnimeGroup Group)> snapshot = await Dispatcher.UIThread.InvokeAsync(
            () => AnimeGroups.Select(g => (g.MalId, g)).ToList());

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

    private void AddEpisodeToGroup(DownloadedEpisode episode, AnimeDetails details)
    {
        AnimeGroup? existing = AnimeGroups.FirstOrDefault(g => g.MalId == details.Id);

        if (existing != null)
        {
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

    private static void InsertEpisodeInSortedOrder(ObservableCollection<DownloadedEpisode> episodes, DownloadedEpisode newEp)
    {
        int insertIndex = 0;
        for (int i = 0; i < episodes.Count; i++)
        {
            DownloadedEpisode existing = episodes[i];
            if (newEp.Season < existing.Season) { insertIndex = i; break; }
            if (newEp.Season == existing.Season && newEp.EpisodeNumber < existing.EpisodeNumber) { insertIndex = i; break; }
            insertIndex = i + 1;
        }
        episodes.Insert(insertIndex, newEp);
    }

    private void UpdateView()
    {
        bool hasContent = AnimeGroups.Count > 0;
        IsEpisodesViewVisible = hasContent;
        IsNoEpisodesViewVisible = !hasContent;
    }

    private void SetupFileWatcher()
    {
        _fileWatcher?.Dispose();
        lock (_debounceLock) { _debounceTimer?.Dispose(); }

        SettingsConfig? config = _saveService.GetSettingsConfig();
        string episodesFolder = config?.EpisodesFolder ?? _saveService.DefaultEpisodesFolder;

        if (!Directory.Exists(episodesFolder))
        {
            try { Directory.CreateDirectory(episodesFolder); }
            catch { return; }
        }

        lock (_debounceLock) { _debounceTimer = new System.Timers.Timer(500); }
        _debounceTimer.AutoReset = false;
        _debounceTimer.Elapsed += async (_, _) =>
        {
            try { await LoadDiskAsync(); }
            catch (Exception ex) { Console.WriteLine(ex); }
        };

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

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        string[] videoExtensions = [".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv"];
        if (!videoExtensions.Contains(Path.GetExtension(e.Name)?.ToLower())) return;
        lock (_debounceLock)
        {
            _debounceTimer?.Stop();
            _debounceTimer?.Start();
        }
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
        AnimeDetailsViewModel detailsVm = DependencyInjection.Instance.ServiceProvider!.GetRequiredService<AnimeDetailsViewModel>();

        await mainVm.GoToAnime(group.MalId);

        detailsVm.SelectedTabIndex = 1;
        detailsVm.TorrentSearchViewModel.TorrentSearchTerms = $"{epNum:D2}";
        _ = detailsVm.TorrentSearchViewModel.SearchTorrentsCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task OpenAnimeDetails(int malId)
    {
        MainViewModel vm = DependencyInjection.Instance.ServiceProvider!.GetRequiredService<MainViewModel>();
        await vm.GoToAnime(malId);
    }

    [RelayCommand]
    private void LaunchEpisode(DownloadedEpisode ep)
    {
        _lastPlayedEpisode = ep;
        _discordService.SetPresenceEpisode(ep.AnimeTitle, ep.EpisodeNumber);
        try
        {
            Process? process = _videoPlayerService.OpenVideo(ep.FilePath);
            if (process != null)
            {
                process.EnableRaisingEvents = true;
                process.Exited += (_, _) => OnVideoPlayerClosed();
            }
        }
        catch (Exception ex) { Console.WriteLine(ex); }
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
        SettingsConfig? config = _saveService.GetSettingsConfig();
        string episodesFolder = config?.EpisodesFolder ?? _saveService.DefaultEpisodesFolder;

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
            if (Avalonia.Application.Current!.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;

            AnimeDetails? animeData = await _animeService.GetFieldsAsync(_lastPlayedEpisode.Id, fields: AnimeField.Episodes);
            if (animeData == null) return;

            ConfirmEpisodeWindow dialog = new()
            {
                DataContext = new ConfirmEpisodeViewModel(_lastPlayedEpisode.EpisodeNumber, animeData.NumEpisodes!.Value)
            };
            bool result = await dialog.ShowDialog<bool>(desktop.MainWindow!);
            if (result) _ = _animeService.SetEpisodesWatchedAsync(_lastPlayedEpisode.Id, _lastPlayedEpisode.EpisodeNumber);
        });

        _discordService.Reset();
    }

    public void Dispose()
    {
        WeakReferenceMessenger.Default.Unregister<SettingsChangedMessage>(this);
        _fileWatcher?.Dispose();
        _debounceTimer?.Dispose();
    }
}
