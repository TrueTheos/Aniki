using System.Collections.ObjectModel;
using Aniki.Services.Interfaces;
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
    private Panel? _originalContainer;
    private VideoView? _videoView;

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

    /// <summary>
    /// Call this from the code-behind to register the VideoView for fullscreen management
    /// </summary>
    public void RegisterVideoView(VideoView videoView, Panel container)
    {
        _videoView = videoView;
        _originalContainer = container;
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
        if (_videoView == null || _originalContainer == null)
        {
            // Fallback to old behavior if not properly initialized
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
        if (_videoView == null || _originalContainer == null) return;

        try
        {
            // Remove VideoView from original container
            _originalContainer.Children.Remove(_videoView);

            // Create fullscreen window
            _fullscreenWindow = new Window
            {
                WindowState = WindowState.FullScreen,
                Background = Avalonia.Media.Brushes.Black,
                SystemDecorations = SystemDecorations.None,
                Title = "Video Player"
            };

            // Create container for video in fullscreen window
            var fullscreenContainer = new Panel
            {
                Background = Avalonia.Media.Brushes.Black
            };
            
            fullscreenContainer.Children.Add(_videoView);
            _fullscreenWindow.Content = fullscreenContainer;

            // Handle window close
            _fullscreenWindow.Closed += (s, e) =>
            {
                if (_fullscreenWindow != null)
                {
                    ExitFullscreen();
                }
            };

            // Handle Escape key to exit fullscreen
            _fullscreenWindow.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    ExitFullscreen();
                }
            };

            IsFullscreen = true;
            _fullscreenWindow.Show();
        }
        catch (Exception ex)
        {
            StatusText = $"Fullscreen error: {ex.Message}";
            // Try to restore if something went wrong
            if (_fullscreenWindow != null)
            {
                _fullscreenWindow.Close();
                _fullscreenWindow = null;
            }
            if (_originalContainer != null && _videoView != null)
            {
                _originalContainer.Children.Add(_videoView);
            }
        }
    }

    private void ExitFullscreen()
    {
        if (_fullscreenWindow == null) return;

        try
        {
            if (_fullscreenWindow.Content is Panel fullscreenContainer)
            {
                fullscreenContainer.Children.Remove(_videoView!);
            }

            var windowToClose = _fullscreenWindow;
            _fullscreenWindow = null;
            windowToClose.Close();

            if (_originalContainer != null && _videoView != null)
            {
                _originalContainer.Children.Add(_videoView);
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
        // Exit fullscreen if active
        if (_fullscreenWindow != null)
        {
            ExitFullscreen();
        }

        _mediaPlayer?.Dispose();
        _libVLC?.Dispose();
    }
}