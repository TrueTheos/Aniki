using System.Collections.ObjectModel;
using Aniki.Services.Interfaces;
using System.Timers;

namespace Aniki.ViewModels;

using LibVLCSharp.Shared;

public partial class OnlineViewModel : ViewModelBase, IDisposable
{
    private readonly IAllMangaScraperService _scraperService;
    private LibVLC? _libVLC;
    private MediaPlayer? _mediaPlayer;
    private System.Timers.Timer? _progressTimer;

    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private ObservableCollection<AllMangaSearchResult> _animeResults = new();

    [ObservableProperty]
    private AllMangaSearchResult? _selectedAnime;

    [ObservableProperty]
    private ObservableCollection<AllManagaEpisode> _episodes = new();

    private AllManagaEpisode? _selectedEpisode;
    
    public AllManagaEpisode? SelectedEpisode
    {
        get => _selectedEpisode;
        set
        {
            if (SetProperty(ref _selectedEpisode, value))
            {
                OnSelectedEpisodeChanged(value);
            }
        }
    }

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private TimeSpan _currentTime;

    [ObservableProperty]
    private TimeSpan _duration;

    [ObservableProperty]
    private double _position;

    [ObservableProperty]
    private bool _isFullscreen;

    public MediaPlayer? MediaPlayer => _mediaPlayer;

    public OnlineViewModel(IAllMangaScraperService scraperService)
    {
        _scraperService = scraperService;
        InitializeVLC();
        InitializeProgressTimer();
    }

    private void InitializeVLC()
    {
        Core.Initialize();
        _libVLC = new LibVLC();
        _mediaPlayer = new MediaPlayer(_libVLC);
        
        // Subscribe to media player events
        _mediaPlayer.TimeChanged += OnTimeChanged;
        _mediaPlayer.LengthChanged += OnLengthChanged;
    }

    private void InitializeProgressTimer()
    {
        _progressTimer = new System.Timers.Timer(1000);
        _progressTimer.Elapsed += OnProgressTimerElapsed;
        _progressTimer.Start();
    }

    private void OnTimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e)
    {
        CurrentTime = TimeSpan.FromMilliseconds(e.Time);
        if (Duration.TotalMilliseconds > 0)
        {
            Position = e.Time;
        }
    }

    private void OnLengthChanged(object? sender, MediaPlayerLengthChangedEventArgs e)
    {
        Duration = TimeSpan.FromMilliseconds(e.Length);
    }

    private void OnProgressTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_mediaPlayer != null && _mediaPlayer.IsPlaying)
        {
            CurrentTime = TimeSpan.FromMilliseconds(_mediaPlayer.Time);
            if (_mediaPlayer.Length > 0)
            {
                Position = _mediaPlayer.Time;
            }
        }
    }

    partial void OnPositionChanged(double value)
    {
        if (_mediaPlayer != null && Math.Abs(_mediaPlayer.Time - value) > 1000)
        {
            _mediaPlayer.Time = (long)value;
        }
    }

    [RelayCommand]
    private async Task SearchAnimeAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
            return;

        IsLoading = true;
        StatusText = "Searching AllManga...";

        try
        {
            var results = await _scraperService.SearchAnimeAsync(SearchQuery);
            AnimeResults.Clear();
            
            foreach (var result in results)
            {
                AnimeResults.Add(result);
            }

            StatusText = $"Found {results.Count} results";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedAnimeChanged(AllMangaSearchResult? value)
    {
        if (value != null)
        {
            _ = LoadEpisodesAsync(value);
        }
    }

    private async Task LoadEpisodesAsync(AllMangaSearchResult anime)
    {
        IsLoading = true;
        StatusText = "Loading episodes...";

        try
        {
            var episodes = await _scraperService.GetEpisodesAsync(anime.Url);
            Episodes.Clear();
            
            foreach (var episode in episodes)
            {
                Episodes.Add(episode);
            }

            StatusText = $"Loaded {episodes.Count} episodes";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OnSelectedEpisodeChanged(AllManagaEpisode? value)
    {
        if (value != null)
        {
            _ = PlayEpisodeAsync(value);
        }
    }

    private async Task PlayEpisodeAsync(AllManagaEpisode episode)
    {
        IsLoading = true;
        StatusText = "Loading video...";

        try
        {
            string videoUrl = await _scraperService.GetVideoUrlAsync(episode.Url!);
            
            Media media = new Media(_libVLC!, new Uri(videoUrl));
            _mediaPlayer?.Play(media);
            
            StatusText = $"Playing Episode {episode.Number}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Play()
    {
        _mediaPlayer?.Play();
    }

    [RelayCommand]
    private void Pause()
    {
        _mediaPlayer?.Pause();
    }

    [RelayCommand]
    private void Stop()
    {
        _mediaPlayer?.Stop();
    }

    [RelayCommand]
    private void ToggleFullscreen()
    {
        IsFullscreen = !IsFullscreen;
        _mediaPlayer!.ToggleFullscreen();
    }

    public void Dispose()
    {
        _progressTimer?.Stop();
        _progressTimer?.Dispose();
        
        if (_mediaPlayer != null)
        {
            _mediaPlayer.TimeChanged -= OnTimeChanged;
            _mediaPlayer.LengthChanged -= OnLengthChanged;
            _mediaPlayer.Dispose();
        }
        
        _libVLC?.Dispose();
    }
}