using System.Collections.ObjectModel;
using System.Diagnostics;
using Aniki.Services.Anime;
using Aniki.Services.Interfaces;
using Aniki.Views;
using Avalonia.Controls.ApplicationLifetimes;

namespace Aniki.ViewModels;

internal sealed class EpisodeRange
{
    public int Start { get; init; }
    public int End { get; init; }
    public string Label => Start == End ? $"Episode {Start}" : $"Episodes {Start}-{End}";
}

internal sealed partial class OnlineViewModel : ViewModelBase, IDisposable
{
    [ObservableProperty] public partial string SearchQuery { get; set; } = "";
    [ObservableProperty] public partial string StatusText { get; set; } = "Search for anime to get started";
    [ObservableProperty] public partial ObservableCollection<AniDbSearchResult> AnimeResults { get; set; } = new();
    [ObservableProperty] public partial string? AnimeDescription { get; set; }
    [ObservableProperty] public partial string? AnimeImageUrl { get; set; }
    [ObservableProperty] public partial ObservableCollection<AniDbEpisode> Episodes { get; set; } = new();
    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial bool CanPlayVideo { get; set; }
    [ObservableProperty] public partial string WatchedEpisodesText { get; set; } = "No episodes watched yet";
    [ObservableProperty] public partial ObservableCollection<EpisodeRange> EpisodeRanges { get; set; } = new();
    [ObservableProperty] public partial ObservableCollection<AniDbEpisode> VisibleEpisodes { get; set; } = new();

    private const int RANGE_SIZE = 50;
    private int _watchedEpisodes;

    public bool HasMultipleRanges => EpisodeRanges.Count > 1;

    private EpisodeRange? _selectedRange;
    public EpisodeRange? SelectedRange
    {
        get => _selectedRange;
        set
        {
            if (!SetProperty(ref _selectedRange, value)) return;
            VisibleEpisodes.Clear();
            if (value == null) return;
            foreach (AniDbEpisode ep in Episodes.Where(e => e.Number >= value.Start && e.Number <= value.End).OrderBy(e => e.Number))
                VisibleEpisodes.Add(ep);
        }
    }

    private void RebuildRanges()
    {
        EpisodeRanges.Clear();
        var ordered = Episodes.OrderBy(e => e.Number).ToList();
        for (int i = 0; i < ordered.Count; i += RANGE_SIZE)
        {
            EpisodeRanges.Add(new EpisodeRange
            {
                Start = ordered[i].Number,
                End = ordered[Math.Min(i + RANGE_SIZE, ordered.Count) - 1].Number
            });
        }

        OnPropertyChanged(nameof(HasMultipleRanges));
        int next = _watchedEpisodes + 1;
        SelectedRange = EpisodeRanges.FirstOrDefault(r => r.Start <= next && next <= r.End) ?? EpisodeRanges.LastOrDefault(r => r.Start <= next) ?? EpisodeRanges.FirstOrDefault();
    }
    
    private AniDbSearchResult? _selectedAnime;
    public AniDbSearchResult? SelectedAnime
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
    
    public AniDbEpisode? SelectedEpisode
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

    private int? _selectedAnimeId;
    private int? _watchingAnimeId;
    
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
    
    private readonly IAniDbScraperService _scraperService;
    private readonly IAnimeService _animeService;
    private readonly IVideoPlayerService _videoPlayerService;
    private readonly IDiscordService _discordService;
    private string? _currentVideoUrl;
    private Process? _videoProcess;

    public bool ShowSelectionStatus => !string.IsNullOrEmpty(SelectionStatusMessage);

    public bool ShowStreamProviderNote =>
        (SelectedAnime != null && !IsLoading && Episodes.Count == 0) ||
        (SelectedEpisode != null && !IsLoading && !CanPlayVideo);

    public OnlineViewModel(IAniDbScraperService scraperService, IAnimeService animeService,
        IVideoPlayerService videoPlayerService, IDiscordService discordService)
    {
        _scraperService = scraperService;
        _animeService = animeService;
        _videoPlayerService = videoPlayerService;
        _discordService = discordService;
        Episodes!.CollectionChanged += OnEpisodesCollectionChanged;
    }

    private void OnEpisodesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            RebuildRanges();
        OnPropertyChanged(nameof(ShowStreamProviderNote));
        OnPropertyChanged(nameof(SelectionStatusMessage));
        OnPropertyChanged(nameof(ShowSelectionStatus));
    }
    
    private ObservableCollection<AniDbSearchResult>? _previousAnimeResults;
    
    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowStreamProviderNote));
        OnPropertyChanged(nameof(SelectionStatusMessage));
        OnPropertyChanged(nameof(ShowSelectionStatus));
    }

    partial void OnCanPlayVideoChanged(bool value) => OnPropertyChanged(nameof(ShowStreamProviderNote));

    partial void OnAnimeResultsChanged(ObservableCollection<AniDbSearchResult> value)
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
        await SearchAnimeAsync(SearchQuery).ConfigureAwait(true);
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
            var results = await _scraperService.SearchAnimeAsync(query).ConfigureAwait(true);
            
            foreach (AniDbSearchResult result in results)
            {
                AnimeResults.Add(result);
            }

            StatusText = results.Count > 0 ? $"Found {results.Count} anime" : "No results. The streaming source may be unavailable right now.";
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

    private async Task OnSelectedAnimeChanged(AniDbSearchResult? value)
    {
        if (value != null)
        {
            SelectedEpisode = null;
            _ = LoadEpisodesAsync(value);

            AnimeImageUrl = value.Banner;
            AnimeDescription = null;
            UpdateWatchedEpisodesText(0);
            _selectedAnimeId = null;

            if (value.MalId is int malId)
            {
                int? animeId = await _animeService.GetIdFromMalIdAsync(malId).ConfigureAwait(true);
                if (value != SelectedAnime || animeId is not int id) return;
                _selectedAnimeId = id;

                AnimeDetails? animeField = await _animeService.GetFieldsAsync(id, fields: [AnimeField.MyListStatus, AnimeField.Synopsis]).ConfigureAwait(true);

                if (animeField != null && value == SelectedAnime)
                {
                    UpdateWatchedEpisodesText(animeField.UserStatus != null ? animeField.UserStatus!.EpisodesWatched : 0);
                    AnimeDescription = animeField.Synopsis;
                    if (!string.IsNullOrEmpty(value.Banner))
                        AnimeImageUrl = value.Banner;
                }
            }
        }
        else
        {
            Episodes.Clear();
            CanPlayVideo = false;
        }
    }

    private async Task LoadEpisodesAsync(AniDbSearchResult anime)
    {
        IsLoading = true;
        StatusText = "Loading episodes...";
        Episodes.Clear();
        CanPlayVideo = false;

        try
        {
            var episodes = await _scraperService.GetEpisodesAsync(anime.Url).ConfigureAwait(true);
            if (anime != SelectedAnime) return;

            foreach (AniDbEpisode episode in episodes)
            {
                Episodes.Add(episode);
            }
            RebuildRanges();

            anime.Episodes = episodes.Count;

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

    private async void OnSelectedEpisodeChanged(AniDbEpisode? value)
    {
        try
        {
            CanPlayVideo = false;
            _currentVideoUrl = null;

            if (value != null)
            {
                await PrepareVideoAsync(value).ConfigureAwait(true);
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
    }

    private async Task PrepareVideoAsync(AniDbEpisode episode)
    {
        IsLoading = true;
        StatusText = "Preparing video...";

        try
        {
            _currentVideoUrl = await _scraperService.GetVideoUrlAsync(episode.Url).ConfigureAwait(true);
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
                _watchingAnimeId = _selectedAnimeId;
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

    private async Task ConfirmEpisodeWatchedAsync(AniDbEpisode episode)
    {
        if (Avalonia.Application.Current!.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;
        if (_watchingAnimeId is not int animeId)
            return;

        AnimeDetails? animeData = await _animeService.GetFieldsAsync(animeId, fields: AnimeField.MyListStatus).ConfigureAwait(true);
        ConfirmEpisodeViewModel confirm = new(episode.Number, episode.TotalEpisodes);
        ConfirmEpisodeWindow dialog = new() { DataContext = confirm };

        bool result = await dialog.ShowDialog<bool>(desktop.MainWindow!).ConfigureAwait(true);

        if (result && confirm.EpisodeNumber > (animeData?.UserStatus?.EpisodesWatched ?? 0))
        {
            _ = _animeService.SetEpisodesWatchedAsync(animeId, confirm.EpisodeNumber);
            UpdateWatchedEpisodesText(confirm.EpisodeNumber);
        }
    }

    private void UpdateWatchedEpisodesText(int episodes)
    {
        _watchedEpisodes = episodes;
        if (SelectedEpisode == null && EpisodeRanges.Count > 1)
        {
            int next = episodes + 1;
            SelectedRange = EpisodeRanges.FirstOrDefault(r => r.Start <= next && next <= r.End) ?? SelectedRange;
        }
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
        if (_selectedAnimeId is int animeId)
        {
            await vm.GoToAnime(animeId).ConfigureAwait(true);
        }
    }

    public async Task GoToAnime(int animeId, string title)
    {
        await SearchAnimeAsync(title).ConfigureAwait(true);

        bool idsAreMal = AnimeService.CurrentProviderType == Services.Auth.ILoginProvider.ProviderType.Mal;
        AniDbSearchResult? matchingAnime = AnimeResults.ToList().FirstOrDefault(x => idsAreMal && x.MalId == animeId)
            ?? AnimeResults.ToList().FirstOrDefault(x =>
                string.Equals(
                    NormalizeTitle(x.Title),
                    NormalizeTitle(title),
                    StringComparison.OrdinalIgnoreCase));

        if (matchingAnime != null)
        {
            SelectedAnime = matchingAnime;
        }
    }

    private static string NormalizeTitle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return string.Join(' ', value
            .Replace("-", " ", StringComparison.Ordinal)
            .Replace(":", " ", StringComparison.Ordinal)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
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