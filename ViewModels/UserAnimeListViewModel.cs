using System.Collections.ObjectModel;
using Aniki.Services.Interfaces;

namespace Aniki.ViewModels;

public partial class UserAnimeListViewModel : ViewModelBase
{
[ObservableProperty]
    private ObservableCollection<AnimeFieldSet> _animeList = new();
    
    [ObservableProperty]
    private ObservableCollection<AnimeFieldSet> _filteredAnimeList = new();
    
    [ObservableProperty]
    private string _searchText = string.Empty;
    
    [ObservableProperty]
    private string _statusFilter = "All";
    
    [ObservableProperty]
    private string _genreFilter = "All Genres";
    
    [ObservableProperty]
    private string _sortBy = "TitleAsc";
    
    [ObservableProperty]
    private bool _isGridView = true;
    
    [ObservableProperty]
    private int _totalCount;
    
    [ObservableProperty]
    private int _filteredCount;
    
    [ObservableProperty]
    private ObservableCollection<string> _availableGenres;

    private IMalService _malService;
    
    public UserAnimeListViewModel(IMalService malService)
    {
        _malService = malService;
        AvailableGenres = new ObservableCollection<string>
        {
            "Action", "Adventure", "Comedy", "Drama", "Fantasy",
            "Horror", "Mystery", "Psychological", "Romance", "Sci-Fi",
            "Slice of Life", "Sports", "Supernatural", "Thriller"
        };
    }

    public override Task Enter()
    {
        _ = LoadAnimeListAsync();
        return base.Enter();
    }

    public string StatusFilterText => StatusFilter == "All" ? "All Status" : FormatStatusDisplay(StatusFilter);
    
    public string GenreFilterText => GenreFilter;
    
    public string SortText => SortBy switch
    {
        "TitleAsc" => "Title (A-Z)",
        "TitleDesc" => "Title (Z-A)",
        "RatingDesc" => "Rating ↓",
        "RatingAsc" => "Rating ↑",
        "MyScoreDesc" => "My Score ↓",
        "MyScoreAsc" => "My Score ↑",
        "Popularity" => "Popularity",
        "DateDesc" => "Newest First",
        "DateAsc" => "Oldest First",
        _ => "Sort By"
    };
    
    public bool HasActiveFilters => 
        StatusFilter != "All" || 
        GenreFilter != "All Genres" || 
        !string.IsNullOrEmpty(SearchText);
    
    partial void OnSearchTextChanged(string value)
    {
        ApplyFiltersAndSort();
    }
    
    partial void OnStatusFilterChanged(string value)
    {
        OnPropertyChanged(nameof(StatusFilterText));
        OnPropertyChanged(nameof(HasActiveFilters));
        ApplyFiltersAndSort();
    }
    
    partial void OnGenreFilterChanged(string value)
    {
        OnPropertyChanged(nameof(GenreFilterText));
        OnPropertyChanged(nameof(HasActiveFilters));
        ApplyFiltersAndSort();
    }
    
    partial void OnSortByChanged(string value)
    {
        OnPropertyChanged(nameof(SortText));
        ApplyFiltersAndSort();
    }
    
    // Commands
    [RelayCommand]
    private void SetStatusFilter(string status)
    {
        StatusFilter = status;
    }
    
    [RelayCommand]
    private void SetGenreFilter(string genre)
    {
        GenreFilter = genre;
    }
    
    [RelayCommand]
    private void ClearGenreFilter()
    {
        GenreFilter = "All Genres";
    }
    
    [RelayCommand]
    private void SetSort(string sortBy)
    {
        SortBy = sortBy;
    }
    
    [RelayCommand]
    private void ClearFilters()
    {
        StatusFilter = "All";
        GenreFilter = "All Genres";
        SearchText = string.Empty;
    }
    
    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
    }
    
    [RelayCommand]
    private void SetGridView()
    {
        IsGridView = true;
    }
    
    [RelayCommand]
    private void SetListView()
    {
        IsGridView = false;
    }
    
    [RelayCommand]
    private void OpenAnimeDetails(int animeId)
    {
        Console.WriteLine($"Opening anime details for ID: {animeId}");
    }
    
    // Methods
    private async Task LoadAnimeListAsync()
    {
        var list = await _malService.GetUserAnimeList();
        foreach (var element in list)
        {
            AnimeList.Add(await _malService.GetFieldsAsync(element.Node.Id, AnimeField.MAIN_PICTURE, AnimeField.MEAN, AnimeField.MY_LIST_STATUS, AnimeField.TITLE, AnimeField.START_DATE, AnimeField.EPISODES));
        }
        
        TotalCount = AnimeList.Count;
        ApplyFiltersAndSort();
    }
    
    private void ApplyFiltersAndSort()
    {
        var filtered = AnimeList.AsEnumerable();
        
        // Apply status filter
        if (StatusFilter != "All")
        {
            filtered = filtered.Where(a => 
                a.MyListStatus?.Status.ToString().Equals(StatusFilter, StringComparison.OrdinalIgnoreCase) == true);
        }
        
        // Apply genre filter
        if (GenreFilter != "All Genres")
        {
            filtered = filtered.Where(a => 
                a.Genres?.Any(g => g.Name.Equals(GenreFilter, StringComparison.OrdinalIgnoreCase)) == true);
        }
        
        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchLower = SearchText.ToLower();
            filtered = filtered.Where(a =>
                a.Title?.ToLower().Contains(searchLower) == true ||
                a.AlternativeTitles?.Ja?.ToLower().Contains(searchLower) == true ||
                a.AlternativeTitles?.En?.ToLower().Contains(searchLower) == true);
        }
        
        // Apply sorting
        filtered = SortBy switch
        {
            "TitleAsc" => filtered.OrderBy(a => a.Title),
            "TitleDesc" => filtered.OrderByDescending(a => a.Title),
            "RatingDesc" => filtered.OrderByDescending(a => a.Mean ?? 0),
            "RatingAsc" => filtered.OrderBy(a => a.Mean ?? 0),
            "MyScoreDesc" => filtered.OrderByDescending(a => a.MyListStatus?.Score ?? 0),
            "MyScoreAsc" => filtered.OrderBy(a => a.MyListStatus?.Score ?? 0),
            "Popularity" => filtered.OrderBy(a => a.Popularity ?? int.MaxValue),
            "DateDesc" => filtered.OrderByDescending(a => a.StartDate),
            "DateAsc" => filtered.OrderBy(a => a.StartDate),
            _ => filtered.OrderBy(a => a.Title)
        };
        
        var result = filtered.ToList();
        FilteredAnimeList = new ObservableCollection<AnimeFieldSet>(result);
        FilteredCount = result.Count;
    }
    
    private static string FormatStatusDisplay(string status)
    {
        return status switch
        {
            "Watching" => "Watching",
            "Completed" => "Completed",
            "OnHold" => "On Hold",
            "Dropped" => "Dropped",
            "PlanToWatch" => "Plan to Watch",
            _ => status
        };
    }
}