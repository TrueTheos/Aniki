using System.Diagnostics;
using Aniki.Services.Interfaces;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;

namespace Aniki.ViewModels;

public partial class AnimeDetailsViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ImageUrl))]
    private MalAnimeDetails? _details;

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
    
    private readonly IMalService _malService;
    
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

    //TODO WE HAVE A WAY TO SUBSCRIBE TO FIELD CHANGES. SUBSCIRE TO MYLISTSTATUS CHANGE TO DETECT DELETION ETC
    
    public AnimeDetailsViewModel(IMalService malService, WatchAnimeViewModel watchAnimeViewModel, TorrentSearchViewModel torrentSearchViewModel) 
    { 
        _malService = malService;
        
        _watchAnimeViewModel = watchAnimeViewModel;
        _torrentSearchViewModel = torrentSearchViewModel;
    }

    public void Update(MalAnimeDetails? details)
    {
        IsLoading = false;
        Details = details;
        WatchedEpisodes = details?.MyListStatus?.NumEpisodesWatched ?? 0;
        SelectedScore = details?.MyListStatus?.Score ?? 0;
        OnPropertyChanged(nameof(ScoreText));
        SelectedStatus = details?.MyListStatus?.Status.ApiToTranslated() ?? AnimeStatusTranslated.Watching;
        
        OnPropertyChanged(nameof(CanIncreaseEpisodeCount));
        OnPropertyChanged(nameof(CanDecreaseEpisodeCount));
        TorrentSearchViewModel.Update(details, WatchedEpisodes);
    }

    public async Task LoadAnimeDetailsAsync(int id)
    {
        IsLoading = true;
        MalAnimeDetails? details = await _malService.GetAllFieldsAsync(id);
        Update(details);

        IsLoading = false;
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

        await _malService.RemoveFromUserList(Details.Id);
            
        await LoadAnimeDetailsAsync(Details.Id);
    }

    [RelayCommand]
    private async Task AddToList()
    {
        if (Details == null) return;
        if (Details.MyListStatus == null || Details.MyListStatus.Status == AnimeStatusApi.none)
        {
            await _malService.SetAnimeStatus(Details.Id, AnimeStatusApi.plan_to_watch);

            await LoadAnimeDetailsAsync(Details.Id);
        }
    }

    private async Task UpdateEpisodeCount(int change)
    {
        if (Details?.MyListStatus == null) return;

        int newCount = WatchedEpisodes + change;

        if (newCount < 0) newCount = 0;

        if (Details.NumEpisodes > 0 && newCount > Details.NumEpisodes)
        {
            newCount = Details.NumEpisodes ?? 0;
        }

        await _malService.SetEpisodesWatched(Details.Id, newCount);
        WatchedEpisodes = newCount;
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
        if (Details?.MyListStatus == null) return;

        await _malService.SetAnimeScore(Details.Id, score);
        Details.MyListStatus.Score = score;
    }

    private async Task UpdateAnimeStatus(AnimeStatusTranslated status)
    {
        if(Details == null) return;
        await _malService.SetAnimeStatus(Details.Id, status.TranslatedToApi());
        if (Details.MyListStatus != null) Details.MyListStatus.Status = status.TranslatedToApi();
    }
    
    [RelayCommand]
    private void OpenMalPage()
    {
        if (Details == null) return;
        string url = $"https://myanimelist.net/anime/{Details.Id}";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
    
    [RelayCommand]
    private void CopyMalPageUrl()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow?.Clipboard is not { } provider)
            return;
        
        if (Details == null) return;

        _ = provider.SetTextAsync($"https://myanimelist.net/anime/{Details.Id}");
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
        
        var mainViewModel = App.ServiceProvider.GetRequiredService<MainViewModel>();
        WatchAnimeViewModel.GoToAnimeInOnlineView(Details.Id, Details.Title);
        _ = mainViewModel.ShowWatchPage();
    }
}
