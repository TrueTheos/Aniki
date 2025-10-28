using Aniki.Misc;
using System.Diagnostics;
using Aniki.Models.MAL;
using Aniki.Services.Interfaces;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace Aniki.ViewModels;

public partial class AnimeDetailsViewModel : ViewModelBase
{
    private MAL_AnimeData? _selectedAnime;
    public MAL_AnimeData? SelectedAnime
    {
        get => _selectedAnime;
        set
        {
            if (SetProperty(ref _selectedAnime, value))
            {
                _ = LoadAnimeDetailsAsync(value);
            }
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ImageUrl))]
    private MAL_AnimeDetails? _details;

    public string? ImageUrl => Details?.MainPicture?.Large;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanIncreaseEpisodeCount))]
    [NotifyPropertyChangedFor(nameof(CanDecreaseEpisodeCount))]
    private int _episodesWatched;

    [ObservableProperty]
    private bool _isLoading;
    
    public bool CanIncreaseEpisodeCount => EpisodesWatched < (Details?.NumEpisodes ?? 0);
    public bool CanDecreaseEpisodeCount => EpisodesWatched > 0;

    [ObservableProperty]
    private WatchAnimeViewModel _watchAnimeViewModel;

    [ObservableProperty]
    private TorrentSearchViewModel _torrentSearchViewModel;
    
    private readonly IMalService _malService;
    
    private int _selectedScore;
    public int SelectedScore
    {
        get => _selectedScore;
        set
        {
            if (SetProperty(ref _selectedScore, value))
            {
                _ = UpdateAnimeScore(value);
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

    public AnimeDetailsViewModel(IMalService malService, WatchAnimeViewModel watchAnimeViewModel, TorrentSearchViewModel torrentSearchViewModel) 
    { 
        _malService = malService;
        
        _watchAnimeViewModel = watchAnimeViewModel;
        _torrentSearchViewModel = torrentSearchViewModel;
    }

    public void Update(MAL_AnimeDetails? details)
    {
        IsLoading = false;
        Details = details;
        EpisodesWatched = details?.MyListStatus?.NumEpisodesWatched ?? 0;
        SelectedScore = details?.MyListStatus?.Score ?? 0;
        SelectedStatus = details?.MyListStatus?.Status.ApiToTranslated() ?? AnimeStatusTranslated.Watching;
        
        OnPropertyChanged(nameof(EpisodesWatched));
        OnPropertyChanged(nameof(CanIncreaseEpisodeCount));
        OnPropertyChanged(nameof(CanDecreaseEpisodeCount));
        WatchAnimeViewModel.Update(details);
        TorrentSearchViewModel.Update(details, EpisodesWatched);
    }

    private async Task LoadAnimeDetailsAsync(MAL_AnimeData? animeData)
    {
        if (animeData?.Node?.Id != null)
        {
            IsLoading = true;
            MAL_AnimeDetails? details = await _malService.GetAnimeDetails(animeData.Node.Id);
            Update(details);

            IsLoading = false;
        }
    }

    [RelayCommand]
    public void IncreaseEpisodeCount()
    {
        _ = UpdateEpisodeCount(1);
    }

    [RelayCommand]
    public void DecreaseEpisodeCount()
    {
        _ = UpdateEpisodeCount(-1);
    }

    [RelayCommand]
    private async Task RemoveFromList()
    {
        if (Details == null) return;

        await _malService.RemoveFromList(Details.Id);
            
        await LoadAnimeDetailsAsync(SelectedAnime);
    }

    [RelayCommand]
    private async Task AddToList()
    {
        if (Details == null) return;
        if (Details.MyListStatus == null || Details.MyListStatus.Status == AnimeStatusApi.none)
        {
            await _malService.UpdateAnimeStatus(Details.Id, AnimeStatusApi.plan_to_watch);

            await LoadAnimeDetailsAsync(SelectedAnime);
        }
    }

    private async Task UpdateEpisodeCount(int change)
    {
        if (Details?.MyListStatus == null) return;

        int newCount = EpisodesWatched + change;

        if (newCount < 0) newCount = 0;

        if (Details.NumEpisodes > 0 && newCount > Details.NumEpisodes)
        {
            newCount = Details.NumEpisodes;
        }

        await _malService.UpdateEpisodesWatched(Details.Id, newCount);
        EpisodesWatched = newCount;
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

        await _malService.UpdateAnimeScore(Details.Id, score);
        Details.MyListStatus.Score = score;
    }

    private async Task UpdateAnimeStatus(AnimeStatusTranslated status)
    {
        if(Details == null) return;
        await _malService.UpdateAnimeStatus(Details.Id, status.TranslatedToApi());
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
    
    public async Task SearchAnimeById(int malId, bool showDetails = true)
    {
        try
        {
            MAL_AnimeDetails? details = await _malService.GetAnimeDetails(malId);

            if (details == null) return;
                
            MAL_AnimeData newMalAnimeData = new()
            {
                Node = new()
                {
                    Id = details.Id,
                    Title = details.Title,
                    Synopsis = details.Synopsis,
                    Status = details.Status,
                    MainPicture = details.MainPicture,
                    Mean = details.Mean
                },
                ListStatus = details.MyListStatus
            };
            SelectedAnime = newMalAnimeData;
        }
        catch (Exception ex)
        {
            Log.Information($"Error searching anime: {ex.Message}");
        }
    }
}
