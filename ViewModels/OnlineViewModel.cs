using System.Collections.ObjectModel;
using Aniki.Services.Interfaces;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using LibVLCSharp.Avalonia;
using LibVLCSharp.Shared;

namespace Aniki.ViewModels;

public partial class OnlineViewModel : ViewModelBase, IDisposable
{
    private readonly IAllMangaScraperService _scraperService;
    private LibVLC? _libVLC;
    private MediaPlayer? _mediaPlayer;
    private Window? _fullscreenWindow;

    private Panel? _videoPlayerContainer; 
    private Border? _originalParent;
    
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
    private bool _isFullscreen;

    public MediaPlayer? MediaPlayer => _mediaPlayer;
    
    public OnlineViewModel(IAllMangaScraperService scraperService)
    {
        _scraperService = scraperService;
        InitializeVLC();
    }

    private void InitializeVLC()
    {
        Core.Initialize();
        _libVLC = new LibVLC();
        _mediaPlayer = new MediaPlayer(_libVLC);
    }

    public void RegisterVideoPlayer(Panel videoContainer, Border originalParent)
    {
        _videoPlayerContainer = videoContainer;
        _originalParent = originalParent;
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
        if (_videoPlayerContainer == null || _originalParent == null)
        {
            _mediaPlayer?.ToggleFullscreen();
            return;
        }

        if (_fullscreenWindow == null)
        {
            EnterFullscreen();
        }
        else
        {
            ExitFullscreen();
        }
    }

    private void EnterFullscreen()
    {
        if (_videoPlayerContainer == null || _originalParent == null) return;

        try
        {
            _originalParent.Child = null;

            _fullscreenWindow = new Window
            {
                WindowState = WindowState.FullScreen,
                Background = Avalonia.Media.Brushes.Black,
                SystemDecorations = SystemDecorations.None,
                Title = "Video Player"
            };

            _fullscreenWindow.AttachDevTools();
            
            _fullscreenWindow.Content = _videoPlayerContainer;
            
            _fullscreenWindow.Closed += (s, e) =>
            {
                if (_fullscreenWindow != null)
                {
                    ExitFullscreen();
                }
            };

            _fullscreenWindow.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    ExitFullscreen();
                }
                else if (e.Key == Key.F12)
                {
                    if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        desktop.MainWindow?.AttachDevTools();
                    }
                }
            };

            IsFullscreen = true;
            _fullscreenWindow.Show();
        }
        catch (Exception ex)
        {
            StatusText = $"Fullscreen error: {ex.Message}";
            if (_fullscreenWindow != null)
            {
                _fullscreenWindow.Close();
                _fullscreenWindow = null;
            }
            if (_originalParent != null && _videoPlayerContainer != null)
            {
                _originalParent.Child = _videoPlayerContainer;
            }
        }
    }

    private void ExitFullscreen()
    {
        if (_fullscreenWindow == null) return;

        try
        {
            _fullscreenWindow.Content = null;

            var windowToClose = _fullscreenWindow;
            _fullscreenWindow = null;
            windowToClose.Close();

            if (_originalParent != null && _videoPlayerContainer != null)
            {
                _originalParent.Child = _videoPlayerContainer;
            }

            IsFullscreen = false;
        }
        catch (Exception ex)
        {
            StatusText = $"Exit fullscreen error: {ex.Message}";
        }
    }

    public void Dispose()
    {
        if (_fullscreenWindow != null)
        {
            ExitFullscreen();
        }

        _mediaPlayer?.Dispose();
        _libVLC?.Dispose();
        
        _originalParent = null;
        _videoPlayerContainer = null;
    }
}