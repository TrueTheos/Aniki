namespace Aniki.Models;

public class AllMangaSearchResult
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Episodes { get; set; } = 0;
    public string Url { get; set; } = string.Empty;
    public int? MalId { get; set; }
    public string? Banner { get; set; }
}

public class AllMangaAnimeDetails
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? MalId { get; set; }
    public int? AniListId { get; set; }
    public string? Description { get; set; }
    public string? Thumbnail { get; set; }
}

public class AllManagaEpisode
{
    public string Id { get; set; } = string.Empty;
    public int Number { get; set; }
    public string Url { get; set; } = string.Empty;
    public string ShowId { get; set; } = string.Empty;
    public string EpisodeString { get; set; } = string.Empty;
    public int TotalEpisodes { get; set; }
}