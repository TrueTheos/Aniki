using System.Collections.ObjectModel;
using System.Diagnostics;
using Aniki.Services.Anime;
using Aniki.Services.Interfaces;

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
    }

    public override Task Enter() => InitializeAsync();

    public Task InitializeAsync()
    {
        // OnAttachedToVisualTree can fire on every navigation back to Browse — load once.
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
        }
        finally
        {
            IsLoading = false;
        }
    }

    private Task LoadHeroAnimeAsync(List<RankingEntry> animeList)
    {
        HeroAnimeList.Clear();

        // Season ranking already includes videos — no per-anime FetchFields.
        foreach (RankingEntry anime in animeList)
        {
            AnimeDetails details = anime.Details;
            if (details.Videos is not { Length: > 0 }) continue;

            AnimeVideo? videoWithThumbnail = details.Videos.FirstOrDefault(x => x.Thumbnail != null);
            if (videoWithThumbnail == null) continue;

            HeroAnimeList.Add(new HeroAnimeData
            {
                AnimeId        = details.Id,
                Title          = details.Title ?? "",
                Synopsis       = details.Synopsis ?? "",
                Score          = details.Mean,
                Status         = details.UserStatus?.Status ?? AnimeStatus.None,
                VideoUrl       = videoWithThumbnail.Url,
                VideoThumbnail = videoWithThumbnail.Thumbnail!,
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