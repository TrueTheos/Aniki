namespace Aniki.Models.Anilist;

public class Anilist_MediaListEntry
{
    public int Id { get; set; }
    public string? Status { get; set; }
    public int? Score { get; set; }
    public int? Progress { get; set; }
    public Anilist_Anime Media { get; set; } = null!;
}