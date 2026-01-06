using System.Collections.ObjectModel;
using System.Diagnostics;
using Aniki.Services.Anime;
using Aniki.Services.Interfaces;

namespace Aniki.ViewModels;

public partial class AnimeBrowseViewModel : ViewModelBase
{
    private readonly IAnimeService _animeService;
    private readonly ICalendarService _calendarService;
    
    [ObservableProperty]
    private ObservableCollection<AnimeCardData> _popularThisSeason = new();
    
    [ObservableProperty]
    private ObservableCollection<AnimeCardData> _popularUpcoming = new();
    
    [ObservableProperty]
    private ObservableCollection<AnimeCardData> _trendingAllTime = new();
    
    [ObservableProperty]
    private ObservableCollection<AnimeCardData> _searchResults = new();

    [ObservableProperty] 
    private ObservableCollection<AnimeCardData> _airingToday = new();
    
    [ObservableProperty]
    private ObservableCollection<HeroAnimeData> _heroAnimeList = new();
    
    [ObservableProperty]
    private HeroAnimeData? _heroAnime;
    
    private int _currentHeroIndex;
    
    [ObservableProperty]
    private string _searchQuery = string.Empty;
    
    public enum AnimeBrowseViewMode {Main, Search}
    
    [ObservableProperty]
    private AnimeBrowseViewMode _viewMode;
    
    [ObservableProperty]
    private bool _isLoading;
    
    [ObservableProperty]
    private int _currentPage = 1;
    
    [ObservableProperty]
    private int _totalPages = 1;
    
    [ObservableProperty]
    private bool _canGoNext;
    
    [ObservableProperty]
    private bool _canGoPrevious;

    private List<AnimeDetails> _allSearchResults = new();
    private const int PAGE_SIZE = 20;

    public AnimeBrowseViewModel(IAnimeService animeService, ICalendarService calendarService)
    {
        _animeService = animeService;
        _calendarService = calendarService;
    }

    public async Task InitializeAsync()
    {
        await LoadCategoriesAsync();
    }

    private async Task LoadCategoriesAsync()
    {
        IsLoading = true;
        try
        {
            List<RankingEntry> airing = await _animeService.GetTopAnimeAsync(RankingCategory.Airing, 20);
            LoadAnimeCards(airing, PopularThisSeason);
            
            await LoadHeroAnimeAsync(airing);

            List<RankingEntry> upcoming = await _animeService.GetTopAnimeAsync(RankingCategory.Upcoming, 20);
            LoadAnimeCards(upcoming, PopularUpcoming);
            
            List<RankingEntry> allTime = await _animeService.GetTopAnimeAsync(RankingCategory.ByPopularity, 20);
            LoadAnimeCards(allTime, TrendingAllTime);

            List<AnimeScheduleItem> airingToday = await _calendarService.GetAnimeScheduleForDayAsync(DateTime.Today);
            AiringToday.Clear();
            foreach (AnimeScheduleItem anime in airingToday)
            {
                if(anime.ProviderId.Keys.Count == 0 || anime.GetId() == null) continue;
                
                AiringToday.Add(new AnimeCardData
                {
                    AnimeId = anime.GetId()!.Value,
                    Title = anime.Title,
                    ImageUrl = anime.ImageUrl,
                    Score = anime.Mean,
                    UserStatus = null
                });
            }
        }
        catch (Exception ex)
        {
            Log.Information($"Error loading categories: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadHeroAnimeAsync(List<RankingEntry> animeList)
    {
        HeroAnimeList.Clear();
        
        foreach (RankingEntry anime in animeList)
        {
            AnimeDetails? details = await _animeService.GetFieldsAsync(anime.Details.Id, fields: [AnimeField.Title, AnimeField.Synopsis, AnimeField.Mean, AnimeField.MyListStatus, AnimeField.Videos]);
            if (details?.Videos != null && details.Videos.Length > 0)
            {
                HeroAnimeData heroData = new()
                {
                    AnimeId = details.Id,
                    Title = details.Title!,
                    Synopsis = details.Synopsis!,
                    Score = details.Mean,
                    Status = details.UserStatus?.Status ?? AnimeStatus.None,
                    VideoUrl = details.Videos[0].Url,
                    VideoThumbnail = details.Videos[0].Thumbnail,
                    IsCurrentHero = HeroAnimeList.Count == 0
                };
                
                HeroAnimeList.Add(heroData);
                
                if (HeroAnimeList.Count >= 5) break;
            }
        }
        
        if (HeroAnimeList.Count > 0)
        {
            HeroAnime = HeroAnimeList[0];
        }
    }

    private void LoadAnimeCards(List<RankingEntry> animeList, ObservableCollection<AnimeCardData> collection)
    {
        collection.Clear();
        foreach (var anime in animeList)
        {
            if (anime.Details != null)
            {
                collection.Add(anime.Details.ToCardData());
            }
        }
    }
    
    [RelayCommand]
    private void PlayHeroVideo()
    {
        if (HeroAnime?.VideoUrl != null)
        {
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
                Log.Information($"Error opening video: {ex.Message}");
            }
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
        await SearchAnimeByTitle(SearchQuery);
    }

    public async Task SearchAnimeByTitle(string query)
    {
        try
        {
            IsLoading = true;
            ViewMode = AnimeBrowseViewMode.Search;

            _allSearchResults = await _animeService.SearchAnimeAsync(query);
            CurrentPage = 1;
            TotalPages = (int)Math.Ceiling(_allSearchResults.Count / (double)PAGE_SIZE);
            
            LoadSearchResultsPage();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Log.Information($"Error searching: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    private void LoadSearchResultsPage()
    {
        SearchResults.Clear();
        
        IEnumerable<AnimeDetails> pageResults = _allSearchResults
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
        if (CanGoNext)
        {
            CurrentPage++;
            LoadSearchResultsPage();
        }
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (CanGoPrevious)
        {
            CurrentPage--;
            LoadSearchResultsPage();
        }
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