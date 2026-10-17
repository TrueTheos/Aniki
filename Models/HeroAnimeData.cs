namespace Aniki.Models;

internal sealed partial class HeroAnimeData : ObservableObject
{
    public int AnimeId { get; set; }
    public required string Title { get; set; }
    public required string Synopsis { get; set; }
    public float Score { get; set; }
    public AnimeStatus Status { get; set; }
    public string? VideoUrl { get; set; }
    public string? PosterUrl { get; set; }
    public string? Backdrop { get; set; }
    public string? Genres { get; set; }
    public string? Meta { get; set; }
    public bool HasVideo => !string.IsNullOrEmpty(VideoUrl);

    [ObservableProperty]
    private bool _isCurrentHero;
}
