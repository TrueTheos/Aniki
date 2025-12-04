using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Aniki.Services.Anime;

namespace Aniki.ViewModels;

public partial class UserAnimeListViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<AnimeDetails> _animeList = new();
    
    [ObservableProperty]
    private ObservableCollection<AnimeDetails> _filteredAnimeList = new();
    
    [ObservableProperty]
    private string _statusFilter = "All";
    
    [ObservableProperty]
    private string _sortBy = "TitleAsc";
    
    [ObservableProperty]
    private bool _isGridView = true;
    
    [ObservableProperty]
    private int _totalCount;
    
    [ObservableProperty]
    private int _filteredCount;
    
    [ObservableProperty]
    private ObservableCollection<GenreViewModel> _availableGenres;

    private IAnimeService _animeService;

    public UserAnimeListViewModel(IAnimeService animeService)
    {
        _animeService = animeService;
        List<string> genres = new()
        {
            "Action", "Adventure", "Comedy", "Drama", "Fantasy",
            "Horror", "Mystery", "Psychological", "Romance", "Sci-Fi",
            "Slice of Life", "Sports", "Supernatural", "Thriller"
        };
        AvailableGenres = new ObservableCollection<GenreViewModel>();
        foreach (string genreName in genres)
        {
            GenreViewModel genreVm = new(genreName);
            genreVm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(GenreViewModel.IsSelected))
                {
                    ApplyFiltersAndSort();
                    OnPropertyChanged(nameof(GenreFilterText));
                    OnPropertyChanged(nameof(HasActiveFilters));
                }
            };
            AvailableGenres.Add(genreVm);
        }
    }

    public override Task Enter()
    {
        _ = LoadAnimeListAsync();
        return base.Enter();
    }

    public string StatusFilterText => StatusFilter == "All" ? "All Status" : FormatStatusDisplay(StatusFilter);

    public string GenreFilterText
    {
        get
        {
            List<GenreViewModel> selectedGenres = AvailableGenres.Where(g => g.IsSelected).ToList();
            if (!selectedGenres.Any()) return "All Genres";
            if (selectedGenres.Count > 2) return $"{selectedGenres.Count} genres selected";
            return string.Join(", ", selectedGenres.Select(g => g.Name));
        }
    }
    
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
        AvailableGenres.Any(g => g.IsSelected);
    
    partial void OnStatusFilterChanged(string value)
    {
        OnPropertyChanged(nameof(StatusFilterText));
        OnPropertyChanged(nameof(HasActiveFilters));
        ApplyFiltersAndSort();
    }
    
    partial void OnSortByChanged(string value)
    {
        OnPropertyChanged(nameof(SortText));
        ApplyFiltersAndSort();
    }
    
    [RelayCommand]
    private void SetStatusFilter(string status)
    {
        StatusFilter = status;
    }
    
    [RelayCommand]
    private void ClearGenreFilter()
    {
        foreach (GenreViewModel genre in AvailableGenres)
        {
            genre.IsSelected = false;
        }
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
        ClearGenreFilter();
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
    
    private async Task LoadAnimeListAsync()
    {
        List<AnimeDetails> list = await _animeService.GetUserAnimeListAsync();
        AnimeList.Clear();
        foreach (AnimeDetails element in list)
        {
            AnimeDetails? details = await _animeService.GetFieldsAsync(element.Id, fields: AnimeService.MalNodeFieldTypes);
            if (details != null) AnimeList.Add(details);
        }
        
        TotalCount = AnimeList.Count;
        ApplyFiltersAndSort();
    }

    private bool CompareStatus(AnimeStatus? api, string statusFilter)
    {
        if (!api.HasValue) return false;
        
        string apiStatus = Regex.Replace(api.Value.ToString().ToLower(), @"[^a-z0-9]", "");
        string filterStatus = Regex.Replace(statusFilter.ToLower(), @"[^a-z0-9]", "");

        return apiStatus == filterStatus;
    }
    
    private void ApplyFiltersAndSort()
    {
        IEnumerable<AnimeDetails> filtered = AnimeList.AsEnumerable();
        
        if (StatusFilter != "All")
        {
            filtered = filtered.Where(a => CompareStatus(a.UserStatus?.Status, StatusFilter));
        }

        List<string> selectedGenres = AvailableGenres.Where(g => g.IsSelected).Select(g => g.Name).ToList();
        if (selectedGenres.Any())
        {
            filtered = filtered.Where(a => 
                a.Genres != null && selectedGenres.All(sg => 
                    a.Genres.Any(ag => ag.Equals(sg, StringComparison.OrdinalIgnoreCase))
                )
            );
        }
        
        filtered = SortBy switch
        {
            "TitleAsc" => filtered.OrderBy(a => a.Title),
            "TitleDesc" => filtered.OrderByDescending(a => a.Title),
            "RatingDesc" => filtered.OrderByDescending(a => a.Mean),
            "RatingAsc" => filtered.OrderBy(a => a.Mean),
            "MyScoreDesc" => filtered.OrderByDescending(a => a.UserStatus?.Score ?? 0),
            "MyScoreAsc" => filtered.OrderBy(a => a.UserStatus?.Score ?? 0),
            "Popularity" => filtered.OrderBy(a => a.Popularity ?? int.MaxValue),
            "DateDesc" => filtered.OrderByDescending(a => a.StartDate),
            "DateAsc" => filtered.OrderBy(a => a.StartDate),
            _ => filtered.OrderBy(a => a.Title)
        };
        
        List<AnimeDetails> result = filtered.ToList();
        FilteredAnimeList = new ObservableCollection<AnimeDetails>(result);
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