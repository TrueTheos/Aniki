using Avalonia.Data.Converters;

namespace Aniki.Misc;

internal enum MediaKind { Anime, Manga }

internal sealed partial class MediaMode : ObservableObject
{
    public static MediaMode Instance { get; } = new();

    [ObservableProperty] public partial MediaKind Kind { get; set; }

    public bool IsManga => Kind == MediaKind.Manga;
    public bool IsAnime => Kind == MediaKind.Anime;
    public string Noun => IsManga ? "Manga" : "Anime";
    public string NounLower => IsManga ? "manga" : "anime";
    public string EmptyHint => IsManga
        ? "Pick a title from Browse, Explore or your Library and it will open here."
        : "Pick a title from Browse, your Library or the Calendar and it will open here.";
    public string Watching => IsManga ? "Reading" : "Watching";
    public string PlanTo => IsManga ? "Plan to Read" : "Plan to Watch";
    public string Watched => IsManga ? "Read" : "Watched";
    public string Units => IsManga ? "chapters" : "episodes";
    public string UnitsLabel => IsManga ? "CHAPTERS" : "EPISODES";
    public string UnitsShort => IsManga ? "ch" : "eps";
    public string Go => IsManga ? "Read!" : "Watch!";
    public string Creator => IsManga ? "Author" : "Studio";
    public string Ongoing => IsManga ? "Publishing" : "Airing";
    public string TopSection => IsManga ? "Top Manga" : "Popular This Season";
    public string SecondSection => IsManga ? "Top Manhwa" : "Popular Upcoming";
    public string HeroBadge => IsManga ? "FEATURED" : "AIRING NOW";

    public static readonly IValueConverter UnitCount = new FuncValueConverter<int?, string>(n =>
        n > 0 ? $"{n} {Instance.Units}" : Instance.Ongoing);
    public static readonly IValueConverter UnitCountShort = new FuncValueConverter<int?, string>(n =>
        n > 0 ? $"{n} {Instance.UnitsShort}" : Instance.Ongoing);
    public static readonly IValueConverter TotalText = new FuncValueConverter<int?, string>(n =>
        n > 0 ? $"Total: {n}" : $"Total: ? ({Instance.Ongoing.ToLowerInvariant()})");
    public static readonly IValueConverter OfTotal = new FuncValueConverter<int?, string>(n =>
        n > 0 ? $"/ {n} {Instance.Units}" : $"{Instance.Units} so far");

    public static readonly IValueConverter StatusText = new FuncValueConverter<object?, string>(v =>
        (v is AnimeStatusTranslated t ? t.TranslatedToAnimeStatus() : v as AnimeStatus?) switch
    {
        AnimeStatus.Watching => Instance.Watching,
        AnimeStatus.PlanToWatch => Instance.PlanTo,
        AnimeStatus.OnHold => "On Hold",
        null or AnimeStatus.None => "",
        { } s => s.ToString()
    });

    partial void OnKindChanged(MediaKind value) => OnPropertyChanged(string.Empty);
}
