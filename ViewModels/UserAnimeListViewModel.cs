using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Aniki.Services.Anime;

namespace Aniki.ViewModels;

public partial class UserAnimeListViewModel : ViewModelBase
{
    [ObservableProperty] public partial ObservableCollection<AnimeDetails> AnimeList { get; set; } = [];
    [ObservableProperty] public partial ObservableCollection<AnimeDetails> FilteredAnimeList { get; set; } = [];
    [ObservableProperty] public partial string StatusFilter { get; set; } = "All";
    [ObservableProperty] public partial string SortBy { get; set; } = "TitleAsc";
    [ObservableProperty] public partial bool IsGridView { get; set; } = true;
    [ObservableProperty] public partial int TotalCount { get; set; }
    [ObservableProperty] public partial int FilteredCount { get; set; }

    [ObservableProperty]
    private ObservableCollection<GenreViewModel> _availableGenres;

    private readonly IAnimeService _animeService;

    public UserAnimeListViewModel(IAnimeService animeService)
    {
        _animeService = animeService;
        List<string> genres =
        [
            "Action", "Adventure", "Comedy", "Drama", "Fantasy",
            "Horror", "Mystery", "Psychological", "Romance", "Sci-Fi",
            "Slice of Life", "Sports", "Supernatural", "Thriller"
        ];
        AvailableGenres = [];
        foreach (GenreViewModel genreVm in genres.Select(genreName => new GenreViewModel(genreName)))
        {
            genreVm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName != nameof(GenreViewModel.IsSelected)) return;
                
                ApplyFiltersAndSort();
                OnPropertyChanged(nameof(GenreFilterText));
                OnPropertyChanged(nameof(HasActiveFilters));
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
            var selectedGenres = AvailableGenres.Where(g => g.IsSelected).ToList();
            return selectedGenres.Count switch
            {
                0   => "All Genres",
                > 2 => $"{selectedGenres.Count} genres selected",
                _   => string.Join(", ", selectedGenres.Select(g => g.Name))
            };
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
        var list = await _animeService.GetUserAnimeListAsync();
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
        var filtered = AnimeList.AsEnumerable();
        
        if (StatusFilter != "All")
        {
            filtered = filtered.Where(a => CompareStatus(a.UserStatus?.Status, StatusFilter));
        }

        var selectedGenres = AvailableGenres.Where(g => g.IsSelected).Select(g => g.Name).ToList();
        if (selectedGenres.Count != 0)
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
        
        var result = filtered.ToList();
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