using Aniki.Misc;

namespace Aniki.Models;

public partial class HeroAnimeData : ObservableObject
{
    public int AnimeId { get; set; }
    public required string Title { get; set; }
    public required string Synopsis { get; set; }
    public float Score { get; set; }
    public AnimeStatusApi Status { get; set; }
    public required string VideoUrl { get; set; }
    public required string VideoThumbnail { get; set; }
    
    [ObservableProperty]
    private bool _isCurrentHero;
}