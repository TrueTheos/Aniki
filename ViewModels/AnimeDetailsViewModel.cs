using System.Diagnostics;
using System.Globalization;
using Aniki.Services;
using Aniki.Services.Anime;
using Aniki.Services.Auth;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace Aniki.ViewModels;

internal sealed partial class AnimeDetailsViewModel : ViewModelBase, IDisposable
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ImageUrl))]
    public partial AnimeDetails? Details { get; set; }

    public string? ImageUrl => Details?.MainPicture?.Large;

    [ObservableProperty]
    public partial int WatchedEpisodes { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsEditingEpisodes { get; set; }

    [ObservableProperty]
    public partial string EpisodesInput { get; set; } = string.Empty;

    public bool CanIncreaseEpisodeCount => Details?.NumEpisodes is not > 0 || WatchedEpisodes < Details.NumEpisodes;
    public bool CanDecreaseEpisodeCount => WatchedEpisodes > 0;

    [ObservableProperty]
    public partial int SelectedTabIndex { get; set; }

    [ObservableProperty]
    public partial WatchAnimeViewModel WatchAnimeViewModel { get; set; }

    [ObservableProperty]
    public partial TorrentSearchViewModel TorrentSearchViewModel { get; set; }

    private readonly IAnimeService _animeService;
    private IAnimeService _service;
    private bool IsManga => !ReferenceEquals(_service, _animeService);


    private int SelectedScore
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                _ = UpdateAnimeScore(value);
                OnPropertyChanged(nameof(ScoreText));
            }
        }
    }
    
    public string ScoreText => SelectedScore == 0 ? "Rate" : SelectedScore.ToString(CultureInfo.InvariantCulture);
    public bool HasVideos => Details?.Videos is { Length: > 0 };

    public AnimeStatusTranslated SelectedStatus
    {
        get;
        set => SetProperty(ref field, value);
    }

    private int? _currentSubscribedId;
    
    private CancellationTokenSource? _episodeUpdateCts;
    
    public AnimeDetailsViewModel(IAnimeService animeService, WatchAnimeViewModel watchAnimeViewModel, TorrentSearchViewModel torrentSearchViewModel) 
    { 
        _animeService = animeService;
        _service = animeService;
        WatchAnimeViewModel = watchAnimeViewModel;
        TorrentSearchViewModel = torrentSearchViewModel;

        MediaMode.Instance.PropertyChanged += (_, _) =>
        {
            if (IsManga != MediaMode.Instance.IsManga) Details = null;
        };
    }

    public override Task Enter()
    {
        SelectedTabIndex = 0;
        return base.Enter();
    }

    public async Task LoadAnimeDetailsAsync(int id)
    {
        IsLoading = true;
        
        ManageSubscriptions(id);

        AnimeDetails? details = await _service.GetAllFieldsAsync(id).ConfigureAwait(true);
        LoadDetails(details);

        IsLoading = false;
    }

    private void ManageSubscriptions(int newId)
    {
        if (_currentSubscribedId.HasValue)
        {
            _service.UnsubscribeFromFieldChange(_currentSubscribedId.Value, OnAnimeDataChanged, AnimeField.MyListStatus);
        }

        _service = _animeService.Current;
        _currentSubscribedId = newId;
        _service.SubscribeToFieldChange(newId, OnAnimeDataChanged, AnimeField.MyListStatus);
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
        OnPropertyChanged(nameof(HasVideos));
        
        SelectedStatus = details?.UserStatus?.Status.ApiToTranslated() ?? AnimeStatusTranslated.Watching;
        
        OnPropertyChanged(nameof(CanIncreaseEpisodeCount));
        OnPropertyChanged(nameof(CanDecreaseEpisodeCount));
        
        if(details != null && !IsManga)
            TorrentSearchViewModel.Update(details);
    }

    [RelayCommand]
    private void IncrementWatchedEpisodes()
    {
        _ = UpdateEpisodeCount(1);
    }

    [RelayCommand]
    private void DecrementWatchedEpisodes()
    {
        _ = UpdateEpisodeCount(-1);
    }

    [RelayCommand]
    private void StartEditingEpisodes()
    {
        if (Details?.UserStatus == null) return;

        EpisodesInput = WatchedEpisodes.ToString(CultureInfo.InvariantCulture);
        IsEditingEpisodes = true;
    }

    [RelayCommand]
    private void CommitEpisodesInput()
    {
        if (!IsEditingEpisodes) return;

        if (Details?.UserStatus == null)
        {
            IsEditingEpisodes = false;
            return;
        }

        if (int.TryParse(EpisodesInput, out int newCount))
        {
            if (newCount < 0) newCount = 0;
            if (Details.NumEpisodes is > 0 && newCount > Details.NumEpisodes)
            {
                newCount = Details.NumEpisodes.Value;
            }

            int delta = newCount - WatchedEpisodes;
            if (delta != 0)
            {
                _ = UpdateEpisodeCount(delta);
            }
        }

        IsEditingEpisodes = false;
    }

    [RelayCommand]
    private void CancelEditingEpisodes()
    {
        IsEditingEpisodes = false;
    }

    [RelayCommand]
    private async Task RemoveFromList()
    {
        if (Details == null) return;

        await _service.RemoveFromUserListAsync(Details.Id).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task AddToList()
    {
        if (Details == null) return;
        if (Details.UserStatus == null || Details.UserStatus.Status == AnimeStatus.None)
        {
            await _service.SetAnimeStatusAsync(Details.Id, AnimeStatus.PlanToWatch).ConfigureAwait(true);
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
        TorrentSearchViewModel.Update(Details);

        _episodeUpdateCts?.CancelAsync();
        _episodeUpdateCts?.Dispose();
        _episodeUpdateCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(2000, _episodeUpdateCts.Token).ConfigureAwait(true);
            await _service.SetEpisodesWatchedAsync(Details.Id, newCount).ConfigureAwait(true);
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
    private async Task UpdateStatus(string status)
    {
        if (Details == null) return;
        await UpdateAnimeStatus(status.ToAnimeStatus()).ConfigureAwait(true);
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

        await _service.SetAnimeScoreAsync(Details.Id, score).ConfigureAwait(true);
        Details.UserStatus.Score = score;
    }

    private async Task UpdateAnimeStatus(AnimeStatusTranslated status)
    {
        if (Details == null) return;

        AnimeStatus animeStatus = status.TranslatedToAnimeStatus();
        try
        {
            await _service.SetAnimeStatusAsync(Details.Id, animeStatus).ConfigureAwait(true);
            Details.UserStatus ??= new UserAnimeStatus();
            Details.UserStatus.Status = animeStatus;
            SelectedStatus = status;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to update status: {ex.Message}");
            await ToastService.Show($"Failed to update status: {ex.Message}").ConfigureAwait(true);
        }
    }

    private static void SaveToClipboard(string? data)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow?.Clipboard is not { } provider)
            return;
        
        if(data == null) return;

        _ = provider.SetTextAsync(data);
        _ = ToastService.Show("✓ Copied!");
    }
    
    [RelayCommand]
    private void OpenMalPage()
    {
        string? url = GetPageUrl();
        if (string.IsNullOrEmpty(url)) return;
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
    
    [RelayCommand]
    private void CopyPageUrl() => SaveToClipboard(GetPageUrl());

    private string? GetPageUrl()
    {
        if (Details == null) return null;
        string type = IsManga ? "manga" : "anime";
        return AnimeService.CurrentProviderType switch
        {
            ILoginProvider.ProviderType.Mal => $"https://myanimelist.net/{type}/{Details.Id}",
            ILoginProvider.ProviderType.AniList => $"https://anilist.co/{type}/{Details.Id}",
            _ => null
        };
    }
    
    [RelayCommand]
    private void CopyAnimeTitle()
    {
        if (Details == null) return;
        SaveToClipboard(Details.Title);
    }

    [RelayCommand]
    private static void OpenUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return;
        
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    [RelayCommand]
    private async Task OpenManga()
    {
        if (Details == null) return;
        MainViewModel mainViewModel = DependencyInjection.Instance.ServiceProvider!.GetRequiredService<MainViewModel>();
        await mainViewModel.OpenSourceManga(Details).ConfigureAwait(true);
    }

    [RelayCommand]
    private void GoToWatchPage()
    {
        if(Details?.Title == null) return;
        
        MainViewModel mainViewModel = DependencyInjection.Instance.ServiceProvider!.GetRequiredService<MainViewModel>();
        if (IsManga)
        {
            _ = mainViewModel.OpenInReader(Details);
            return;
        }
        WatchAnimeViewModel.GoToAnimeInOnlineView(Details.Id, Details.Title);
        _ = mainViewModel.ShowWatchPage();
    }

    public void Dispose()
    {
        _episodeUpdateCts?.Dispose();
    }
}
