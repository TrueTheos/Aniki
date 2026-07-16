using System.Collections.ObjectModel;
using System.Diagnostics;
using Aniki.Misc;
using Aniki.Services.Anime;
using Aniki.Services.Interfaces;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;

namespace Aniki.ViewModels;

internal sealed partial class AnimeBrowseViewModel : ViewModelBase
{
    private readonly IAnimeService _animeService;
    private readonly ICalendarService _calendarService;

    [ObservableProperty]
    public partial ObservableCollection<AnimeCardData> PopularThisSeason { get; set; } = [];

    [ObservableProperty] public partial ObservableCollection<AnimeCardData> PopularUpcoming { get; set; } = [];

    [ObservableProperty] public partial ObservableCollection<AnimeCardData> TrendingAllTime { get; set; } = [];

    [ObservableProperty] public partial ObservableCollection<AnimeCardData> SearchResults { get; set; } = [];

    [ObservableProperty] public partial ObservableCollection<AnimeCardData> AiringToday { get; set; } = [];

    [ObservableProperty] public partial ObservableCollection<HeroAnimeData> HeroAnimeList { get; set; } = [];

    [ObservableProperty] public partial HeroAnimeData? HeroAnime { get; set; }


    [ObservableProperty] public partial string SearchQuery { get; set; } = string.Empty;

    internal enum AnimeBrowseViewMode {Main, Search}

    [ObservableProperty] public partial AnimeBrowseViewMode ViewMode { get; set; }

    [ObservableProperty] public partial bool IsLoading { get; set; }

    [ObservableProperty] public partial int CurrentPage { get; set; } = 1;

    [ObservableProperty] public partial int TotalPages { get; set; } = 1;

    [ObservableProperty] public partial bool CanGoNext { get; set; }

    [ObservableProperty] public partial bool CanGoPrevious { get; set; }

    private List<AnimeDetails> _allSearchResults = [];
    private int _currentHeroIndex;
    private Task? _categoriesLoadTask;
    
    private const int PAGE_SIZE = 20;

    public AnimeBrowseViewModel(IAnimeService animeService, ICalendarService calendarService)
    {
        _animeService = animeService;
        _calendarService = calendarService;

        WeakReferenceMessenger.Default.Register<CacheClearedMessage>(this, (_, _) =>
        {
            _categoriesLoadTask = null;
            PopularThisSeason.Clear();
            PopularUpcoming.Clear();
            TrendingAllTime.Clear();
            AiringToday.Clear();
            HeroAnimeList.Clear();
            HeroAnime = null;
        });

        WeakReferenceMessenger.Default.Register<UserListStatusChangedMessage>(this, (_, msg) =>
        {
            Dispatcher.UIThread.Post(() => ApplyStatusToCards(msg.AnimeId, msg.Status));
        });
    }

    public override Task Enter() => InitializeAsync();

    public Task InitializeAsync()
    {
        if (_categoriesLoadTask is { IsCompletedSuccessfully: true })
            return Task.CompletedTask;

        if (_categoriesLoadTask is { IsCompleted: false })
            return _categoriesLoadTask;

        _categoriesLoadTask = LoadCategoriesAsync();
        return _categoriesLoadTask;
    }

    private async Task LoadCategoriesAsync()
    {
        IsLoading = true;
        try
        {
            var airing = await _animeService.GetTopAnimeAsync(RankingCategory.Airing).ConfigureAwait(true);
            LoadAnimeCards(airing, PopularThisSeason);

            await LoadHeroAnimeAsync(airing).ConfigureAwait(true);

            var upcoming = await _animeService.GetTopAnimeAsync(RankingCategory.Upcoming).ConfigureAwait(true);
            LoadAnimeCards(upcoming, PopularUpcoming);

            var allTime = await _animeService.GetTopAnimeAsync(RankingCategory.ByPopularity).ConfigureAwait(true);
            LoadAnimeCards(allTime, TrendingAllTime);

            var airingToday = await _calendarService.GetAnimeScheduleForDayAsync(DateTime.Today).ConfigureAwait(true);
            AiringToday.Clear();
            foreach (AnimeScheduleItem anime in airingToday)
            {
                if (anime.ProviderId.Keys.Count == 0 || anime.GetId() == null) continue;

                AiringToday.Add(new AnimeCardData
                {
                    AnimeId    = anime.GetId()!.Value,
                    Title      = anime.Title,
                    ImageUrl   = anime.ImageUrl,
                    Score      = anime.Mean,
                    UserStatus = null
                });
            }

            _ = ApplyUserListStatusesAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ApplyUserListStatusesAsync()
    {
        if (!AnimeService.IsLoggedIn) return;

        try
        {
            var list = await _animeService.GetUserAnimeListAsync().ConfigureAwait(true);
            Dictionary<int, AnimeStatus?> byId = list.ToDictionary(a => a.Id, a => a.UserStatus?.Status);

            foreach (AnimeCardData card in EnumerateCards())
                card.UserStatus = byId.TryGetValue(card.AnimeId, out AnimeStatus? status) ? status : null;

            foreach (HeroAnimeData hero in HeroAnimeList)
            {
                hero.Status = byId.TryGetValue(hero.AnimeId, out AnimeStatus? status) && status.HasValue
                    ? status.Value
                    : AnimeStatus.None;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to apply list statuses to browse cards: {ex.Message}");
        }
    }

    private void ApplyStatusToCards(int animeId, AnimeStatus? status)
    {
        foreach (AnimeCardData card in EnumerateCards())
        {
            if (card.AnimeId == animeId)
                card.UserStatus = status;
        }

        foreach (HeroAnimeData hero in HeroAnimeList)
        {
            if (hero.AnimeId == animeId)
                hero.Status = status ?? AnimeStatus.None;
        }

        if (HeroAnime?.AnimeId == animeId)
            HeroAnime.Status = status ?? AnimeStatus.None;
    }

    private IEnumerable<AnimeCardData> EnumerateCards() =>
        PopularThisSeason
            .Concat(PopularUpcoming)
            .Concat(TrendingAllTime)
            .Concat(AiringToday)
            .Concat(SearchResults);

    private Task LoadHeroAnimeAsync(List<RankingEntry> animeList)
    {
        HeroAnimeList.Clear();

        foreach (RankingEntry entry in animeList)
        {
            AnimeDetails details = entry.Details;
            AnimeVideo? video = details.Videos?.FirstOrDefault(x => !string.IsNullOrEmpty(x.Thumbnail));
            if (video == null) continue;

            HeroAnimeList.Add(new HeroAnimeData
            {
                AnimeId        = details.Id,
                Title          = details.Title ?? "",
                Synopsis       = details.Synopsis ?? "",
                Score          = details.Mean,
                Status         = details.UserStatus?.Status ?? AnimeStatus.None,
                VideoUrl       = video.Url,
                VideoThumbnail = video.Thumbnail!,
                IsCurrentHero  = HeroAnimeList.Count == 0
            });

            if (HeroAnimeList.Count >= 5) break;
        }

        if (HeroAnimeList.Count > 0)
            HeroAnime = HeroAnimeList[0];

        return Task.CompletedTask;
    }

    private static void LoadAnimeCards(List<RankingEntry> animeList, ObservableCollection<AnimeCardData> collection)
    {
        collection.Clear();
        foreach (RankingEntry anime in animeList)
        {
            collection.Add(anime.Details.ToCardData());
        }
    }
    
    [RelayCommand]
    private void PlayHeroVideo()
    {
        if (HeroAnime?.VideoUrl == null) return;
        
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = HeroAnime.VideoUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error opening video: {ex.Message}");
        }
    }
    
    [RelayCommand]
    private void NextHero()
    {
        if (HeroAnimeList.Count == 0) return;
        
        HeroAnimeList[_currentHeroIndex].IsCurrentHero = false;
        _currentHeroIndex = (_currentHeroIndex + 1) % HeroAnimeList.Count;
        HeroAnimeList[_currentHeroIndex].IsCurrentHero = true;
        HeroAnime = HeroAnimeList[_currentHeroIndex];
    }
    
    [RelayCommand]
    private void PreviousHero()
    {
        if (HeroAnimeList.Count == 0) return;
        
        HeroAnimeList[_currentHeroIndex].IsCurrentHero = false;
        _currentHeroIndex = (_currentHeroIndex - 1 + HeroAnimeList.Count) % HeroAnimeList.Count;
        HeroAnimeList[_currentHeroIndex].IsCurrentHero = true;
        HeroAnime = HeroAnimeList[_currentHeroIndex];
    }
    
    [RelayCommand]
    private async Task PerformSearchAsync()
    {
        await SearchAnimeByTitle(SearchQuery).ConfigureAwait(true);
    }

    public async Task SearchAnimeByTitle(string query)
    {
        IsLoading = true;
        ViewMode  = AnimeBrowseViewMode.Search;

        _allSearchResults = await _animeService.SearchAnimeAsync(query).ConfigureAwait(true);
        CurrentPage       = 1;
        TotalPages        = (int)Math.Ceiling(_allSearchResults.Count / (double)PAGE_SIZE);
            
        LoadSearchResultsPage();
        IsLoading = false;
    }
    
    private void LoadSearchResultsPage()
    {
        SearchResults.Clear();
        
        var pageResults = _allSearchResults
                          .Skip((CurrentPage - 1) * PAGE_SIZE)
                          .Take(PAGE_SIZE);

        foreach (AnimeDetails result in pageResults)
        {
            SearchResults.Add(result.ToCardData());
        }

        UpdatePaginationState();
    }
    
    private void UpdatePaginationState()
    {
        CanGoPrevious = CurrentPage > 1;
        CanGoNext = CurrentPage < TotalPages;
    }

    [RelayCommand]
    private void NextPage()
    {
        if (!CanGoNext) return;
        
        CurrentPage++;
        LoadSearchResultsPage();
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (!CanGoPrevious) return;
        
        CurrentPage--;
        LoadSearchResultsPage();
    }

    [RelayCommand]
    private void GoBack()
    {
        SearchQuery = string.Empty;
        ExitSearchMode();
    }

    private void ExitSearchMode()
    {
        ViewMode = AnimeBrowseViewMode.Main;
        SearchResults.Clear();
        _allSearchResults.Clear();
    }
}