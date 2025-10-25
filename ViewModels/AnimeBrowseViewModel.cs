    using Aniki.Services.Interfaces;
    using System.Collections.ObjectModel;

    namespace Aniki.ViewModels;

    public partial class AnimeBrowseViewModel : ViewModelBase
    {
        private readonly IMalService _malService;
        
        [ObservableProperty]
        private ObservableCollection<AnimeCardData> _popularThisSeason = new();
        
        [ObservableProperty]
        private ObservableCollection<AnimeCardData> _popularUpcoming = new();
        
        [ObservableProperty]
        private ObservableCollection<AnimeCardData> _trendingAllTime = new();
        
        [ObservableProperty]
        private ObservableCollection<AnimeCardData> _searchResults = new();
        
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

        public AnimeBrowseViewModel(IMalService malService)
        {
            _malService = malService;
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

        private async Task LoadAnimeCards(List<MAL_RankingEntry> animeList, ObservableCollection<AnimeCardData> collection)
        {
            collection.Clear();
            foreach (var anime in animeList)
            {
                var details = await _malService.GetAnimeDetails(anime.Node.Id);
                if (details != null)
                {
                    var card = new AnimeCardData
                    {
                        AnimeId = details.Id,
                        Title = details.Title,
                        Status = details.MyListStatus?.Status
                    };
                    _ = LoadAnimeImageAsync(card, details.MainPicture);
                    collection.Add(card);
                }
            }
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
                
                await LoadSearchResultsPage();
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
        
        private async Task LoadSearchResultsPage()
        {
            SearchResults.Clear();
            
            var pageResults = _allSearchResults
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize);

            foreach (var result in pageResults)
            {
                var card = new AnimeCardData
                {
                    AnimeId = result.MalAnime.Id,
                    Title = result.MalAnime.Title,
                    Status = null
                };
                
                await LoadAnimeImageAsync(card, result.MalAnime.MainPicture);
                
                SearchResults.Add(card);
            }

            UpdatePaginationState();
        }

        private async Task LoadAnimeImageAsync(AnimeCardData card, MAL_MainPicture? picture)
        {
            if (picture != null)
            {
                card.Image = await _malService.GetAnimeImage(picture);
            }
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
                _ = LoadSearchResultsPage();
            }
        }

        [RelayCommand]
        private void PreviousPage()
        {
            if (CanGoPrevious)
            {
                CurrentPage--;
                _ = LoadSearchResultsPage();
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