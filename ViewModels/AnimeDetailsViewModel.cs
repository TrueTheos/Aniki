using System.Diagnostics;
using Aniki.Services.Anime;
using Aniki.Services.Auth;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace Aniki.ViewModels;

public partial class AnimeDetailsViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ImageUrl))]
    private AnimeDetails? _details;

    public string? ImageUrl => Details?.MainPicture?.Large;

    [ObservableProperty]
    private int _watchedEpisodes;

    [ObservableProperty]
    private bool _isLoading;
    
    public bool CanIncreaseEpisodeCount => WatchedEpisodes < (Details?.NumEpisodes ?? 0);
    public bool CanDecreaseEpisodeCount => WatchedEpisodes > 0;

    [ObservableProperty]
    private WatchAnimeViewModel _watchAnimeViewModel;

    [ObservableProperty]
    private TorrentSearchViewModel _torrentSearchViewModel;
    
    private readonly IAnimeService _animeService;
    
    public string ScoreText => SelectedScore == 0 ? "Rate" : SelectedScore.ToString();

    private int _selectedScore;
    public int SelectedScore
    {
        get => _selectedScore;
        set
        {
            if (SetProperty(ref _selectedScore, value))
            {
                _ = UpdateAnimeScore(value);
                OnPropertyChanged(nameof(ScoreText));
            }
        }
    }
    
    private AnimeStatusTranslated _selectedStatus;
    public AnimeStatusTranslated SelectedStatus
    {
        get => _selectedStatus;
        set
        {
            if (SetProperty(ref _selectedStatus, value))
            {
                _ = UpdateAnimeStatus(value);
            }
        }
    }

    private int? _currentSubscribedId;
    
    private CancellationTokenSource? _episodeUpdateCts;
    
    public AnimeDetailsViewModel(IAnimeService animeService, WatchAnimeViewModel watchAnimeViewModel, TorrentSearchViewModel torrentSearchViewModel) 
    { 
        _animeService = animeService;
        _watchAnimeViewModel = watchAnimeViewModel;
        _torrentSearchViewModel = torrentSearchViewModel;
    }
    
    public async Task LoadAnimeDetailsAsync(int id)
    {
        IsLoading = true;
        
        ManageSubscriptions(id);

        AnimeDetails? details = await _animeService.GetAllFieldsAsync(id);
        LoadDetails(details);

        IsLoading = false;
    }

    private void ManageSubscriptions(int newId)
    {
        if (_currentSubscribedId.HasValue && _currentSubscribedId != newId)
        {
            _animeService.UnsubscribeFromFieldChange(_currentSubscribedId.Value, OnAnimeDataChanged, AnimeField.MyListStatus);
        }

        _currentSubscribedId = newId;
        _animeService.SubscribeToFieldChange(newId, OnAnimeDataChanged, AnimeField.MyListStatus);
    }

    private void OnAnimeDataChanged(AnimeDetails updatedEntity)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            if (Details == null || Details.Id != updatedEntity.Id) return;

            LoadDetails(updatedEntity);
            
            OnPropertyChanged(nameof(Details));
        });
    }

    private void LoadDetails(AnimeDetails? details)
    {
        Details = details;
        
        WatchedEpisodes = details?.UserStatus?.EpisodesWatched ?? 0;
        SelectedScore = details?.UserStatus?.Score ?? 0;
        OnPropertyChanged(nameof(ScoreText));
        
        SelectedStatus = details?.UserStatus?.Status.ApiToTranslated() ?? AnimeStatusTranslated.Watching;
        
        OnPropertyChanged(nameof(CanIncreaseEpisodeCount));
        OnPropertyChanged(nameof(CanDecreaseEpisodeCount));
        
        if(details != null)
            TorrentSearchViewModel.Update(details, WatchedEpisodes);
    }

    [RelayCommand]
    public void IncrementWatchedEpisodes()
    {
        _ = UpdateEpisodeCount(1);
    }

    [RelayCommand]
    public void DecrementWatchedEpisodes()
    {
        _ = UpdateEpisodeCount(-1);
    }

    [RelayCommand]
    private async Task RemoveFromList()
    {
        if (Details == null) return;

        await _animeService.RemoveFromUserListAsync(Details.Id);
    }

    [RelayCommand]
    private async Task AddToList()
    {
        if (Details == null) return;
        if (Details.UserStatus == null || Details.UserStatus.Status == AnimeStatus.None)
        {
            await _animeService.SetAnimeStatusAsync(Details.Id, AnimeStatus.PlanToWatch);
        }
    }

    private async Task UpdateEpisodeCount(int change)
    {
        if (Details?.UserStatus == null) return;

        int newCount = WatchedEpisodes + change;

        if (newCount < 0) newCount = 0;
        if (Details.NumEpisodes > 0 && newCount > Details.NumEpisodes)
        {
            newCount = Details.NumEpisodes ?? 0;
        }

        WatchedEpisodes = newCount; 
        
        OnPropertyChanged(nameof(CanIncreaseEpisodeCount));
        OnPropertyChanged(nameof(CanDecreaseEpisodeCount));
        TorrentSearchViewModel.Update(Details, WatchedEpisodes);

        _episodeUpdateCts?.Cancel();
        _episodeUpdateCts?.Dispose();
        _episodeUpdateCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(2000, _episodeUpdateCts.Token);
            await _animeService.SetEpisodesWatchedAsync(Details.Id, newCount);
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to update episodes: {ex.Message}");
        }
    }
    
    [RelayCommand]
    private void UpdateStatus(string status)
    {
        if (Details == null) return;
    
        SelectedStatus = status.ToAnimeStatus();
    }

    [RelayCommand]
    private void UpdateScore(string scoreStr)
    {
        if (Details == null) return;
        if (int.TryParse(scoreStr, out int score))
        {
            SelectedScore = score;
        }
    }
    private async Task UpdateAnimeScore(int score)
    {
        if (Details?.UserStatus == null) return;

        await _animeService.SetAnimeScoreAsync(Details.Id, score);
        Details.UserStatus.Score = score;
    }

    private async Task UpdateAnimeStatus(AnimeStatusTranslated status)
    {
        if(Details?.UserStatus == null) return;
        
        await _animeService.SetAnimeStatusAsync(Details.Id, status.TranslatedToAnimeStatus());
        if (Details.UserStatus != null) Details.UserStatus.Status = status.TranslatedToAnimeStatus();
    }
    
    [RelayCommand]
    private void OpenMalPage()
    {
        if (Details == null) return;
        string url = "";
        switch (AnimeService.CurrentProviderType)
        {
            case ILoginProvider.ProviderType.Mal:
                url = $"https://myanimelist.net/anime/{Details.Id}";
                break;
            case ILoginProvider.ProviderType.AniList:
                url = $"https://anilist.com/anime/{Details.Id}";
                break;
            default:
                //todo do something
                break;
        }

        if (string.IsNullOrEmpty(url)) return;
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
    
    [RelayCommand]
    private void CopyPageUrl()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow?.Clipboard is not { } provider)
            return;
        
        if (Details == null) return;
        
        switch (AnimeService.CurrentProviderType)
        {
            case ILoginProvider.ProviderType.Mal:
                _ = provider.SetTextAsync($"https://myanimelist.net/anime/{Details.Id}");
                break;
            case ILoginProvider.ProviderType.AniList:
                _ = provider.SetTextAsync($"https://anilist.com/anime/{Details.Id}");
                break;
            default:
                //todo do something
                break;
        }
    }

    [RelayCommand]
    private void OpenUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return;
        
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    [RelayCommand]
    private void GoToWatchPage()
    {
        if(Details == null) return;
        if(Details.Title == null) return;
        
        MainViewModel mainViewModel = DependencyInjection.Instance.ServiceProvider!.GetRequiredService<MainViewModel>();
        WatchAnimeViewModel.GoToAnimeInOnlineView(Details.Id, Details.Title);
        _ = mainViewModel.ShowWatchPage();
    }
}
