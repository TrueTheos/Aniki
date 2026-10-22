using System.Collections.ObjectModel;
using System.ComponentModel;
using Aniki.Models.Anilist;
using Aniki.Services.Anime;
using Aniki.Services.Anime.Providers;
using Aniki.Services.Auth;
using Avalonia.Threading;

namespace Aniki.ViewModels;

internal sealed partial class ExploreViewModel : ViewModelBase
{
    internal sealed record Option(string Label, string? Format = null, string? Country = null, string? Value = null)
    {
        public override string ToString() => Label;
    }

    private static readonly string[] GenreNames =
    [
        "Action", "Adventure", "Comedy", "Drama", "Ecchi", "Fantasy", "Horror", "Mahou Shoujo", "Mecha", "Music",
        "Mystery", "Psychological", "Romance", "Sci-Fi", "Slice of Life", "Sports", "Supernatural", "Thriller"
    ];

    private static readonly Option[] MangaFormats =
    [
        new("All formats"), new("Manga", "MANGA", "JP"), new("Manhwa", "MANGA", "KR"), new("Manhua", "MANGA", "CN"),
        new("One-shot", "ONE_SHOT"), new("Light novel", "NOVEL")
    ];

    private static readonly Option[] AnimeFormats =
    [
        new("All formats"), new("TV", "TV"), new("Movie", "MOVIE"), new("OVA", "OVA"), new("ONA", "ONA"), new("Special", "SPECIAL")
    ];

    public ObservableCollection<GenreViewModel> Genres { get; } = new(GenreNames.Select(g => new GenreViewModel(g)));
    public ObservableCollection<string> SelectedTags { get; } = [];
    [ObservableProperty] public partial List<string> AllTags { get; set; } = [];
    [ObservableProperty] public partial string? PickedTag { get; set; }
    [ObservableProperty] public partial string? TagQuery { get; set; }

    [ObservableProperty] public partial Option[] Formats { get; set; } = MangaFormats;
    [ObservableProperty] public partial Option SelectedFormat { get; set; } = MangaFormats[0];

    public Option[] Statuses { get; } =
    [
        new("Any status"), new("Ongoing", Value: "RELEASING"), new("Finished", Value: "FINISHED"),
        new("Not yet released", Value: "NOT_YET_RELEASED"), new("On hiatus", Value: "HIATUS")
    ];
    [ObservableProperty] public partial Option SelectedStatus { get; set; }

    public Option[] Sorts { get; } =
    [
        new("Most popular", Value: "POPULARITY_DESC"), new("Top rated", Value: "SCORE_DESC"),
        new("Trending now", Value: "TRENDING_DESC"), new("Newest", Value: "START_DATE_DESC")
    ];
    [ObservableProperty] public partial Option SelectedSort { get; set; }

    [ObservableProperty] public partial ObservableCollection<AnimeCardData> Results { get; set; } = [];
    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial bool HasMore { get; set; }
    [ObservableProperty] public partial bool HasNoResults { get; set; }

    private readonly AnilistService _anilist;
    private readonly IAnimeService _animeService;
    private MediaKind? _loadedKind;
    private int _page;
    private int _searchVersion;
    private bool _suspendSearch;

    public ExploreViewModel(AnilistService anilist, IAnimeService animeService)
    {
        _anilist = anilist;
        _animeService = animeService;
        SelectedStatus = Statuses[0];
        SelectedSort = Sorts[0];

        foreach (GenreViewModel genre in Genres)
            genre.PropertyChanged += OnFilterChanged;
        SelectedTags.CollectionChanged += (_, _) => _ = SearchAsync();
    }

    public override async Task Enter()
    {
        if (AllTags.Count == 0) AllTags = await _anilist.GetTagsAsync().ConfigureAwait(true);

        if (_loadedKind != MediaMode.Instance.Kind)
        {
            _loadedKind = MediaMode.Instance.Kind;
            _suspendSearch = true;
            Formats = MediaMode.Instance.IsManga ? MangaFormats : AnimeFormats;
            SelectedFormat = Formats[0];
            _suspendSearch = false;
            await SearchAsync().ConfigureAwait(true);
        }
    }

    public void PresetGenre(string genre)
    {
        _suspendSearch = true;
        foreach (GenreViewModel g in Genres) g.IsSelected = g.Name == genre;
        SelectedTags.Clear();
        _suspendSearch = false;
        _loadedKind = null;
    }

    private void OnFilterChanged(object? sender, PropertyChangedEventArgs e) => _ = SearchAsync();
    partial void OnSelectedFormatChanged(Option value) => _ = SearchAsync();
    partial void OnSelectedStatusChanged(Option value) => _ = SearchAsync();
    partial void OnSelectedSortChanged(Option value) => _ = SearchAsync();

    partial void OnPickedTagChanged(string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        if (!SelectedTags.Contains(value)) SelectedTags.Add(value);
        Dispatcher.UIThread.Post(() => { PickedTag = null; TagQuery = ""; });
    }

    [RelayCommand]
    private void RemoveTag(string tag) => SelectedTags.Remove(tag);

    [RelayCommand]
    private async Task ClearFilters()
    {
        _suspendSearch = true;
        foreach (GenreViewModel g in Genres) g.IsSelected = false;
        SelectedTags.Clear();
        SelectedFormat = Formats[0];
        SelectedStatus = Statuses[0];
        _suspendSearch = false;
        await SearchAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private Task LoadMore() => LoadPageAsync(_page + 1, _searchVersion);

    private Task SearchAsync()
    {
        if (_suspendSearch || _loadedKind == null) return Task.CompletedTask;
        Results = [];
        return LoadPageAsync(1, ++_searchVersion);
    }

    private async Task LoadPageAsync(int page, int version)
    {
        IsLoading = true;
        try
        {
            (List<Anilist_Anime> items, bool hasMore) = await _anilist.DiscoverAsync(
                MediaMode.Instance.Kind,
                Genres.Where(g => g.IsSelected).Select(g => g.Name).ToList(),
                SelectedTags.ToList(),
                SelectedFormat.Format, SelectedFormat.Country, SelectedStatus.Value,
                SelectedSort.Value!, page).ConfigureAwait(true);
            if (version != _searchVersion) return; // a newer search started meanwhile

            Dictionary<int, AnimeStatus?> statuses = await GetListStatusesAsync().ConfigureAwait(true);
            bool mal = AnimeService.CurrentProviderType == ILoginProvider.ProviderType.Mal;
            foreach (Anilist_Anime m in items)
            {
                if ((mal ? m.IdMal : m.Id) is not { } id) continue;
                Results.Add(new AnimeCardData
                {
                    AnimeId = id,
                    Title = m.Title?.Romaji ?? m.Title?.English,
                    ImageUrl = m.CoverImage?.Large ?? m.CoverImage?.Medium,
                    Score = (m.MeanScore ?? 0) / 10f,
                    UserStatus = statuses.GetValueOrDefault(id)
                });
            }

            _page = page;
            HasMore = hasMore;
            HasNoResults = Results.Count == 0;
        }
        finally
        {
            if (version == _searchVersion) IsLoading = false;
        }
    }

    private async Task<Dictionary<int, AnimeStatus?>> GetListStatusesAsync()
    {
        if (!AnimeService.IsLoggedIn) return [];
        try
        {
            var list = await _animeService.Current.GetUserAnimeListAsync().ConfigureAwait(true);
            return list.ToDictionary(a => a.Id, a => a.UserStatus?.Status);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Explore: couldn't read list statuses: {ex.Message}");
            return [];
        }
    }
}
