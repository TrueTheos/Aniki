using Aniki.Misc;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Aniki.ViewModels;

public partial class AnimeDetailsViewModel : ViewModelBase
{
    private readonly NyaaService _nyaaService = new();
    private AnimeData? _selectedAnime;
    public AnimeData? SelectedAnime
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
    private AnimeDetails? _details;

    [ObservableProperty]
    private ObservableCollection<AnimeData> _animeList;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanIncreaseEpisodeCount))]
    [NotifyPropertyChangedFor(nameof(CanDecreaseEpisodeCount))]
    private int _episodesWatched;

    [ObservableProperty]
    private bool _isLoading;

    private int _nextEpisodeNumber = -1;

    public int NextEpisodeNumber
    {
        get => _nextEpisodeNumber == -1 ? EpisodesWatched + 1 : _nextEpisodeNumber;
        set
        {
            if (_nextEpisodeNumber != value) 
            {
                _nextEpisodeNumber = value;
                OnPropertyChanged(nameof(NextEpisodeNumber));
            }
        }
    }

    public bool CanIncreaseEpisodeCount => EpisodesWatched < (Details?.NumEpisodes ?? 0);
    public bool CanDecreaseEpisodeCount => EpisodesWatched > 0;

    [ObservableProperty]
    private string _torrentSearchTerms = string.Empty;

    [ObservableProperty]
    private ObservableCollection<NyaaTorrent> _torrentsList = new();

    private int _selectedScore;
    public int SelectedScore
    {
        get => _selectedScore;
        set
        {
            if (SetProperty(ref _selectedScore, value))
            {
                _ = UpdateAnimeScore(value.ToString());
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

    private AnimeStatusTranslated _selectedFilter;
    public AnimeStatusTranslated SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (SetProperty(ref _selectedFilter, value))
            {
                _ = LoadAnimeListAsync(value);
            }
        }
    }

    public IReadOnlyList<AnimeStatusTranslated> StatusOptions { get; } =
    [
        AnimeStatusTranslated.Watching, AnimeStatusTranslated.Completed, AnimeStatusTranslated.OnHold,
        AnimeStatusTranslated.Dropped, AnimeStatusTranslated.PlanToWatch
    ];

    public IEnumerable<AnimeStatusTranslated> FilterOptions => [.. Enum.GetValues<AnimeStatusTranslated>()];

    public List<int> ScoreOptions { get; } = new() {0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

    [ObservableProperty]
    public ObservableCollection<int> _watchEpisodesOptions = new();

    [ObservableProperty]
    private WatchAnimeViewModel _watchAnimeViewModel;

    public AnimeDetailsViewModel() 
    { 
        WatchAnimeViewModel = new();
        _animeList = new();
        SelectedFilter = AnimeStatusTranslated.All;
    }

    public override async Task Enter()
    {
        await LoadAnimeListAsync(AnimeStatusTranslated.All);
    }

    public void Update(AnimeDetails? details)
    {
        IsLoading = false;
        Details = details;
        EpisodesWatched = details?.MyListStatus?.NumEpisodesWatched ?? 0;

        WatchEpisodesOptions = new();
        for (int i = 0; i < details?.NumEpisodes; i++)
        {
            WatchEpisodesOptions.Add(i + 1);
        }
            
        OnPropertyChanged(nameof(EpisodesWatched));
        OnPropertyChanged(nameof(NextEpisodeNumber));
        OnPropertyChanged(nameof(CanIncreaseEpisodeCount));
        OnPropertyChanged(nameof(CanDecreaseEpisodeCount));
        SelectedScore = details?.MyListStatus?.Score ?? 1;
        SelectedStatus = details?.MyListStatus != null ? details.MyListStatus.Status.ApiToTranslated() : AnimeStatusTranslated.All;
        WatchAnimeViewModel.Update(details);
    }

    private async Task LoadAnimeDetailsAsync(AnimeData? animeData)
    {
        if (animeData?.Node?.Id != null)
        {
            IsLoading = true;
            AnimeDetails? details = await MalUtils.GetAnimeDetails(animeData.Node.Id);
            Update(details);

            IsLoading = false;
        }
    }

    public async Task LoadAnimeListAsync(AnimeStatusTranslated filter)
    {
        try
        {
            IsLoading = true;
            AnimeList.Clear();

            List<AnimeData> animeListData = await MalUtils.LoadAnimeList(filter.TranslatedToApi());

            foreach (AnimeData anime in animeListData)
            {
                AnimeList.Add(anime);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading anime list: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task<List<SearchEntry>> SearchAnimeByTitle(string searchQuery, bool fillList = true, bool showFirstBest = false)
    {
        List<SearchEntry> results = await MalUtils.SearchAnimeOrdered(searchQuery);
        AnimeList.Clear();
            
        if (fillList)
        {
            AnimeList.Clear();
            foreach (SearchEntry entry in results)
            {
                AnimeData newAnimeData = new()
                {
                    Node = new()
                    {
                        Id = entry.Anime.Id,
                        Title = entry.Anime.Title
                    },
                    ListStatus = null
                };
                AnimeList.Add(newAnimeData);
            }
        }

        if (showFirstBest) SelectedAnime = AnimeList[0];
            
        return results;
    }
        
    public async Task SearchAnimeById(int malId, bool showDetails = true)
    {
        try
        {
            AnimeDetails? details = await MalUtils.GetAnimeDetails(malId);
            AnimeList.Clear();

            if (details == null) return;
                
            AnimeData newAnimeData = new()
            {
                Node = new()
                {
                    Id = details.Id,
                    Title = details.Title
                },
                ListStatus = details.MyListStatus
            };
            AnimeList.Add(newAnimeData);
            SelectedAnime = newAnimeData;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error searching anime: {ex.Message}");
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

        await MalUtils.RemoveFromList(Details.Id);
            
        await LoadAnimeDetailsAsync(SelectedAnime);
    }

    [RelayCommand]
    private async Task AddToList()
    {
        if (Details == null) return;

        await MalUtils.UpdateAnimeStatus(Details.Id, MalUtils.AnimeStatusField.STATUS, "watching");
            
        await LoadAnimeDetailsAsync(SelectedAnime);
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

        await MalUtils.UpdateAnimeStatus(Details.Id, MalUtils.AnimeStatusField.EPISODES_WATCHED, newCount.ToString());
        EpisodesWatched = newCount;
    }

    private async Task UpdateAnimeScore(string score)
    {
        if (Details?.MyListStatus == null) return;
        if (score == null) return;

        await MalUtils.UpdateAnimeStatus(Details.Id, MalUtils.AnimeStatusField.SCORE, score);
        Details.MyListStatus.Score = int.Parse(score);
    }

    private async Task UpdateAnimeStatus(AnimeStatusTranslated status)
    {
        if(Details == null) return;
        await MalUtils.UpdateAnimeStatus(Details.Id, MalUtils.AnimeStatusField.STATUS, status.TranslatedToApi().ToString());
        if (Details.MyListStatus != null) Details.MyListStatus.Status = status.TranslatedToApi();
    }

    [RelayCommand]
    public async Task SearchTorrents()
    {
        if (Details == null) return;

        TorrentsList.Clear();

        List<NyaaTorrent> list = await _nyaaService.SearchAsync(Details.Title, NextEpisodeNumber);

        foreach (NyaaTorrent t in list)
        {
            TorrentsList.Add(t);
        }
    }

    [RelayCommand]
    public void DownloadTorrent(string magnet)
    {
        Process.Start(new ProcessStartInfo(magnet) { UseShellExecute = true });
    }

    [RelayCommand]
    private void OpenMalPage()
    {
        if (Details == null) return;
        string url = $"https://myanimelist.net/anime/{Details.Id}";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}