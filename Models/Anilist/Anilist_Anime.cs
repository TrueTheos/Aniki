namespace Aniki.Models.Anilist;

public class Anilist_Anime
{
    public int Id { get; set; }
    public Anilist_Title? Title { get; set; }
    public Anilist_CoverImage? CoverImage { get; set; }
    public string? Status { get; set; }
    public string? Description { get; set; }
    public int? Episodes { get; set; }
    public int? MeanScore { get; set; }
    public int? Popularity { get; set; }
    public Anilist_Studios? Studios { get; set; }
    public Anilist_Date? StartDate { get; set; }
    public List<string>? Genres { get; set; }
    public int? Favourites { get; set; }
    public Anilist_Trailer? Trailer { get; set; }
    public Anilist_Relations? Relations { get; set; }
    public Anilist_MediaListStatus? MediaListEntry { get; set; }
    public Anilist_Stats? Stats { get; set; }
    public string? Format { get; set; }
}