using Aniki.Services.Interfaces;
using System.Collections.ObjectModel;
using Aniki.Models.MAL;
using System.Diagnostics;
using Aniki.Misc;

namespace Aniki.ViewModels;

public partial class AnimeBrowseViewModel : ViewModelBase
{
    private readonly IMalService _malService;
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
    
    // Hero Carousel Properties
    [ObservableProperty]
    private ObservableCollection<HeroAnimeData> _heroAnimeList = new();
    
    [ObservableProperty]
    private HeroAnimeData? _heroAnime;
    
    private int _currentHeroIndex = 0;
    
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

    private List<MAL_SearchEntry> _allSearchResults = new();
    private const int PageSize = 20;

    public AnimeBrowseViewModel(IMalService malService, ICalendarService calendarService)
    {
        _malService = malService;
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
            var airing = await _malService.GetTopAnimeInCategory(MalService.AnimeRankingCategory.AIRING);
            await LoadAnimeCards(airing, PopularThisSeason);

            var upcoming = await _malService.GetTopAnimeInCategory(MalService.AnimeRankingCategory.UPCOMING);
            await LoadAnimeCards(upcoming, PopularUpcoming);
            
            var allTime = await _malService.GetTopAnimeInCategory(MalService.AnimeRankingCategory.BYPOPULARITY);
            await LoadAnimeCards(allTime, TrendingAllTime);

            var airingToday = await _calendarService.GetAnimeScheduleForDayAsync(DateTime.Today);
            foreach (var anime in airingToday)
            {
                if(anime.MalId == null) continue;
                MAL_AnimeDetails? details = await _malService.GetAnimeDetails(anime.MalId.Value);
                if(details != null) AiringToday.Add(details.ToCardData());
            }
            
            await LoadHeroAnimeAsync(airing);
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

    private async Task LoadHeroAnimeAsync(List<MAL_RankingEntry> animeList)
    {
        HeroAnimeList.Clear();
        
        foreach (var anime in animeList.Take(10))
        {
            var details = await _malService.GetAnimeDetails(anime.Node.Id, true);
            if (details?.Videos != null && details.Videos.Length > 0)
            {
                var heroData = new HeroAnimeData
                {
                    AnimeId = details.Id,
                    Title = details.Title,
                    Synopsis = details.Synopsis,
                    Score = details.Mean,
                    Status = details.MyListStatus?.Status ?? AnimeStatusApi.none,
                    VideoUrl = details.Videos[0].Url,
                    VideoThumbnail = details.Videos[0].Thumbnail,
                    IsCurrentHero = HeroAnimeList.Count == 0
                };
                
                HeroAnimeList.Add(heroData);
                
                if (HeroAnimeList.Count >= 5) break; // Limit to 5 hero anime
            }
        }
        
        if (HeroAnimeList.Count > 0)
        {
            HeroAnime = HeroAnimeList[0];
        }
    }

    private async Task LoadAnimeCards(List<MAL_RankingEntry> animeList, ObservableCollection<AnimeCardData> collection)
    {
        collection.Clear();
        foreach (var anime in animeList)
        {
            var details = await _malService.GetAnimeDetails(anime.Node.Id);
            if (details != null)
            {
                collection.Add(details.ToCardData());
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

            _allSearchResults = await _malService.SearchAnimeOrdered(query);
            CurrentPage = 1;
            TotalPages = (int)Math.Ceiling(_allSearchResults.Count / (double)PageSize);
            
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
        
        var pageResults = _allSearchResults
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize);

        foreach (var result in pageResults)
        {
            MAL_MainPicture? picture = result.MalAnime.MainPicture;
            var card = new AnimeCardData
            {
                AnimeId = result.MalAnime.Id,
                Title = result.MalAnime.Title,
                Status = null,
                ImageUrl = picture!.Large != null ? picture.Large : picture.Medium
            };
            
            SearchResults.Add(card);
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