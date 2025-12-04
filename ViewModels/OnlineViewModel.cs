using System.Collections.ObjectModel;
using System.Diagnostics;
using Aniki.Services.Anime;
using Aniki.Services.Interfaces;
using Aniki.Views;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;

namespace Aniki.ViewModels;

public partial class OnlineViewModel : ViewModelBase, IDisposable
{
    private readonly IAllMangaScraperService _scraperService;
    private readonly IAnimeService _animeService;
    private readonly IVideoPlayerService _videoPlayerService;
    private readonly IDiscordService _discordService;
    private string? _currentVideoUrl;
    private Process? _videoProcess;
    
    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private string _statusText = "Search for anime to get started";

    [ObservableProperty]
    private ObservableCollection<AllMangaSearchResult> _animeResults = new();

    private AllMangaSearchResult? _selectedAnime;
    public AllMangaSearchResult? SelectedAnime
    {
        get => _selectedAnime;
        set
        {
            if (SetProperty(ref _selectedAnime, value))
            {
                OnPropertyChanged(nameof(SelectionStatusMessage));
                OnPropertyChanged(nameof(ShowSelectionStatus));
                _ = OnSelectedAnimeChanged(value);
            }
        }
    }

    [ObservableProperty] 
    private string? _animeDescription;

    [ObservableProperty] 
    private string? _animeImageUrl;

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
                OnPropertyChanged(nameof(SelectionStatusMessage));
                OnPropertyChanged(nameof(ShowSelectionStatus));
                OnSelectedEpisodeChanged(value);
            }
        }
    }

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _canPlayVideo;

    [ObservableProperty]
    private string _watchedEpisodesText = "No episodes watched yet";

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
                return "Select episode";
        
            return "";
        }
    }

    public bool ShowSelectionStatus => !string.IsNullOrEmpty(SelectionStatusMessage);

    public OnlineViewModel(IAllMangaScraperService scraperService, IAnimeService animeService,
        IVideoPlayerService videoPlayerService, IDiscordService discordService)
    {
        _watchingMalId = null;
        _scraperService = scraperService;
        _animeService = animeService;
        _videoPlayerService = videoPlayerService;
        _discordService = discordService;
    }
    
    partial void OnAnimeResultsChanged(ObservableCollection<AllMangaSearchResult> value)
    {
        OnPropertyChanged(nameof(SelectionStatusMessage));
        OnPropertyChanged(nameof(ShowSelectionStatus));
    
        if (value != null)
        {
            value.CollectionChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(SelectionStatusMessage));
                OnPropertyChanged(nameof(ShowSelectionStatus));
            };
        }
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
            List<AllMangaSearchResult> results = await _scraperService.SearchAnimeAsync(query);
            
            foreach (AllMangaSearchResult result in results)
            {
                AnimeResults.Add(result);
            }

            StatusText = results.Count > 0 ? $"Found {results.Count} anime" : "No results found";
        }
        catch (Exception ex)
        {
            StatusText = $"Search error: {ex.Message}";
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
            _ = LoadEpisodesAsync(value);

            AnimeDetails? animeField = await _animeService.GetFieldsAsync(value.MalId!.Value, fields: [AnimeField.MyListStatus, AnimeField.Synopsis]);

            if (animeField != null)
            {
                if (animeField.UserStatus != null)
                {
                    UpdateWatchedEpisodesText(animeField.UserStatus!.EpisodesWatched);
                }
                else
                {
                    UpdateWatchedEpisodesText(0);
                }

                AnimeDescription = animeField.Synopsis;
                AnimeImageUrl = value.Banner;
            }
            else
            {
                //todo this shouldn't happen :)
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
            List<AllManagaEpisode> episodes = await _scraperService.GetEpisodesAsync(anime.Url);
            
            foreach (AllManagaEpisode episode in episodes)
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

    private async void OnSelectedEpisodeChanged(AllManagaEpisode? value)
    {
        CanPlayVideo = false;
        _currentVideoUrl = null;

        if (value != null)
        {
            await PrepareVideoAsync(value);
        }
    }

    private async Task PrepareVideoAsync(AllManagaEpisode episode)
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
            if (_videoProcess != null && !_videoProcess.HasExited)
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
        if (SelectedEpisode != null)
        {
            if (AnimeService.IsLoggedIn)
            {
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    if (Avalonia.Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        ConfirmEpisodeWindow dialog = new() 
                        {
                            DataContext = new ConfirmEpisodeViewModel(SelectedEpisode.Number, SelectedEpisode.TotalEpisodes)
                        };


                        bool result = await dialog.ShowDialog<bool>(desktop.MainWindow!);

                        if (result)
                        {
                            if (SelectedEpisode != null)
                            {
                                _ = _animeService.SetEpisodesWatchedAsync(_watchingMalId!.Value, SelectedEpisode.Number);
                                UpdateWatchedEpisodesText(SelectedEpisode.Number);
                            }
                        }
                    }
                });
            }
            
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                StatusText = $"Finished watching Episode {SelectedEpisode.Number}";
            });
        }

        _discordService.Reset();
        _videoProcess?.Dispose();
        _videoProcess = null;
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
    private void GoToAnimeDetails()
    {
        MainViewModel vm = DependencyInjection.Instance.ServiceProvider!.GetRequiredService<MainViewModel>();
        if (_selectedAnime != null && _selectedAnime.MalId != null)
        {
            vm.GoToAnime(_selectedAnime.MalId.Value);
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
        if (_videoProcess != null)
        {
            _videoProcess.Exited -= OnVideoProcessExited;
            _videoProcess.Dispose();
        }
        
        _currentVideoUrl = null;
    }
}