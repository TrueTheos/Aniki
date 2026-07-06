using System.Collections.ObjectModel;
using System.Diagnostics;
using Aniki.Services.Anime;
using Aniki.Services.Interfaces;
using Aniki.Views;
using Avalonia.Controls.ApplicationLifetimes;

namespace Aniki.ViewModels;

public partial class OnlineViewModel : ViewModelBase, IDisposable
{
    [ObservableProperty] public partial string SearchQuery { get; set; } = "";
    [ObservableProperty] public partial string StatusText { get; set; } = "Search for anime to get started";
    [ObservableProperty] public partial ObservableCollection<AllMangaSearchResult> AnimeResults { get; set; } = new();
    [ObservableProperty] public partial string? AnimeDescription { get; set; }
    [ObservableProperty] public partial string? AnimeImageUrl { get; set; }
    [ObservableProperty] public partial ObservableCollection<AllMangaEpisode> Episodes { get; set; } = new();
    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial bool CanPlayVideo { get; set; }
    [ObservableProperty] public partial string WatchedEpisodesText { get; set; } = "No episodes watched yet";
    
    private AllMangaSearchResult? _selectedAnime;
    public AllMangaSearchResult? SelectedAnime
    {
        get => _selectedAnime;
        set
        {
            if (!SetProperty(ref _selectedAnime, value)) return;
            
            OnPropertyChanged(nameof(SelectionStatusMessage));
            OnPropertyChanged(nameof(ShowSelectionStatus));
            OnPropertyChanged(nameof(ShowStreamProviderNote));
            _ = OnSelectedAnimeChanged(value);
        }
    }
    
    public AllMangaEpisode? SelectedEpisode
    {
        get;
        set
        {
            if (!SetProperty(ref field, value)) return;
            
            OnPropertyChanged(nameof(SelectionStatusMessage));
            OnPropertyChanged(nameof(ShowSelectionStatus));
            OnPropertyChanged(nameof(ShowStreamProviderNote));
            OnSelectedEpisodeChanged(value);
        }
    }

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

    private int? _watchingMalId;
    
    public string SelectionStatusMessage
    {
        get
        {
            if (AnimeResults.Count == 0)
                return "Search anime";
        
            if (SelectedAnime == null)
                return "Select anime";
        
            if (SelectedEpisode == null)
            {
                if (!IsLoading && Episodes.Count == 0)
                    return "No episodes";
                return "Select episode";
            }
        
            return "";
        }
    }
    
    private readonly IAllMangaScraperService _scraperService;
    private readonly IAnimeService _animeService;
    private readonly IVideoPlayerService _videoPlayerService;
    private readonly IDiscordService _discordService;
    private string? _currentVideoUrl;
    private Process? _videoProcess;

    public bool ShowSelectionStatus => !string.IsNullOrEmpty(SelectionStatusMessage);

    public bool ShowStreamProviderNote =>
        (SelectedAnime != null && !IsLoading && Episodes.Count == 0) ||
        (SelectedEpisode != null && !IsLoading && !CanPlayVideo);

    public OnlineViewModel(IAllMangaScraperService scraperService, IAnimeService animeService,
        IVideoPlayerService videoPlayerService, IDiscordService discordService)
    {
        _watchingMalId = null;
        _scraperService = scraperService;
        _animeService = animeService;
        _videoPlayerService = videoPlayerService;
        _discordService = discordService;
        Episodes!.CollectionChanged += OnEpisodesCollectionChanged;
    }

    private void OnEpisodesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(ShowStreamProviderNote));
        OnPropertyChanged(nameof(SelectionStatusMessage));
        OnPropertyChanged(nameof(ShowSelectionStatus));
    }
    
    private ObservableCollection<AllMangaSearchResult>? _previousAnimeResults;
    
    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowStreamProviderNote));
        OnPropertyChanged(nameof(SelectionStatusMessage));
        OnPropertyChanged(nameof(ShowSelectionStatus));
    }

    partial void OnCanPlayVideoChanged(bool value) => OnPropertyChanged(nameof(ShowStreamProviderNote));

    partial void OnAnimeResultsChanged(ObservableCollection<AllMangaSearchResult> value)
    {
        OnPropertyChanged(nameof(SelectionStatusMessage));
        OnPropertyChanged(nameof(ShowSelectionStatus));
    
        if (_previousAnimeResults != null)
            _previousAnimeResults.CollectionChanged -= OnAnimeResultsCollectionChanged;
        
        _previousAnimeResults = value;
        
        value.CollectionChanged += OnAnimeResultsCollectionChanged;
    }
    
    private void OnAnimeResultsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(SelectionStatusMessage));
        OnPropertyChanged(nameof(ShowSelectionStatus));
    }

    [RelayCommand]
    private async Task SearchAnimeAsync()
    {
        await SearchAnimeAsync(SearchQuery);
    }

    private async Task SearchAnimeAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return;

        IsLoading = true;
        StatusText = "Searching anime...";
        AnimeResults.Clear();
        Episodes.Clear();
        SelectedAnime = null;
        CanPlayVideo = false;

        try
        {
            var results = await _scraperService.SearchAnimeAsync(query);
            
            foreach (AllMangaSearchResult result in results)
            {
                AnimeResults.Add(result);
            }

            StatusText = results.Count > 0 ? $"Found {results.Count} anime" : "No results found";
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task OnSelectedAnimeChanged(AllMangaSearchResult? value)
    {
        if (value != null)
        {
            SelectedEpisode = null;
            _ = LoadEpisodesAsync(value);

            AnimeDetails? animeField = await _animeService.GetFieldsAsync(value.MalId!.Value, fields: [AnimeField.MyListStatus, AnimeField.Synopsis]);

            if (animeField != null)
            {
                UpdateWatchedEpisodesText(animeField.UserStatus != null ? animeField.UserStatus!.EpisodesWatched : 0);

                AnimeDescription = animeField.Synopsis;
                AnimeImageUrl = value.Banner;
            }
        }
        else
        {
            Episodes.Clear();
            CanPlayVideo = false;
        }
    }

    private async Task LoadEpisodesAsync(AllMangaSearchResult anime)
    {
        IsLoading = true;
        StatusText = "Loading episodes...";
        Episodes.Clear();
        CanPlayVideo = false;

        try
        {
            var episodes = await _scraperService.GetEpisodesAsync(anime.Url);
            
            foreach (AllMangaEpisode episode in episodes)
            {
                Episodes.Add(episode);
            }

            StatusText = episodes.Count > 0
                ? $"Loaded {episodes.Count} episodes - Select one to watch"
                : "No episodes available";
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading episodes: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async void OnSelectedEpisodeChanged(AllMangaEpisode? value)
    {
        try
        {
            CanPlayVideo = false;
            _currentVideoUrl = null;

            if (value != null)
            {
                await PrepareVideoAsync(value);
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
    }

    private async Task PrepareVideoAsync(AllMangaEpisode episode)
    {
        IsLoading = true;
        StatusText = "Preparing video...";

        try
        {
            _currentVideoUrl = await _scraperService.GetVideoUrlAsync(episode.Url);
            CanPlayVideo = !string.IsNullOrEmpty(_currentVideoUrl);
            StatusText = $"Episode {episode.Number} ready - Click 'Play' to watch";
        }
        catch (Exception ex)
        {
            StatusText = $"Error preparing video: {ex.Message}";
            CanPlayVideo = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void PlayInBrowser()
    {
        if (string.IsNullOrEmpty(_currentVideoUrl) || SelectedEpisode == null)
        {
            StatusText = "No video URL available";
            return;
        }

        try
        {
            if (_videoProcess is { HasExited: false })
            {
                _videoProcess.Exited -= OnVideoProcessExited;
            }

            _videoProcess = _videoPlayerService.OpenVideo(_currentVideoUrl);
            
            if (_videoProcess != null)
            {
                _discordService.SetPresenceEpisode(SelectedAnime!.Title, SelectedEpisode.Number);
                _watchingMalId = SelectedAnime!.MalId;
                _videoProcess.EnableRaisingEvents = true;
                _videoProcess.Exited += OnVideoProcessExited;
                
                string playerName = SelectedPlayer?.DisplayName ?? "video player";
                StatusText = $"Playing Episode {SelectedEpisode.Number} in {playerName}";
            }
            else
            {
                StatusText = "Video player opened";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error opening video player: {ex.Message}";
            Debug.WriteLine(ex);
        }
    }

    private void OnVideoProcessExited(object? sender, EventArgs e)
    {
        if (SelectedEpisode is { } episode)
        {
            if (AnimeService.IsLoggedIn)
            {
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => ConfirmEpisodeWatchedAsync(episode));
            }

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                StatusText = $"Finished watching Episode {episode.Number}";
            });
        }

        _discordService.Reset();
        _videoProcess?.Dispose();
        _videoProcess = null;
    }

    private async Task ConfirmEpisodeWatchedAsync(AllMangaEpisode episode)
    {
        if (Avalonia.Application.Current!.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        ConfirmEpisodeWindow dialog = new()
        {
            DataContext = new ConfirmEpisodeViewModel(episode.Number, episode.TotalEpisodes)
        };

        bool result = await dialog.ShowDialog<bool>(desktop.MainWindow!);

        if (result)
        {
            _ = _animeService.SetEpisodesWatchedAsync(_watchingMalId!.Value, episode.Number);
            UpdateWatchedEpisodesText(episode.Number);
        }
    }

    private void UpdateWatchedEpisodesText(int episodes)
    {
        WatchedEpisodesText = episodes switch
        {
            0 => "No episodes watched yet",
            1 => $"1 / {SelectedAnime!.Episodes} episode watched",
            _ => $"{episodes} / {SelectedAnime!.Episodes} episodes watched"
        };
    }

    [RelayCommand]
    private async Task GoToAnimeDetails()
    {
        MainViewModel vm = DependencyInjection.Instance.ServiceProvider!.GetRequiredService<MainViewModel>();
        if (_selectedAnime != null && _selectedAnime.MalId != null)
        {
            await vm.GoToAnime(_selectedAnime.MalId.Value);
        }
    }

    public async Task GoToAnime(int malId, string title)
    {
        await SearchAnimeAsync(title);

        AllMangaSearchResult? matchingAnime = AnimeResults.ToList().FirstOrDefault(x => x.MalId != null && x.MalId.Value == malId);
        if (matchingAnime != null)
        {
            SelectedAnime = matchingAnime;
        }
    }

    public void Dispose()
    {
        Episodes.CollectionChanged -= OnEpisodesCollectionChanged;

        if (_previousAnimeResults != null)
            _previousAnimeResults.CollectionChanged -= OnAnimeResultsCollectionChanged;
        
        if (_videoProcess != null)
        {
            _videoProcess.Exited -= OnVideoProcessExited;
            _videoProcess.Dispose();
        }
        
        _currentVideoUrl = null;
    }
}