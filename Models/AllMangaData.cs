namespace Aniki.Models;

public class AllMangaSearchResult
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
}

public class AllManagaEpisode
{
    public string? Id { get; set; }
    public int Number { get; set; }
    public string? Url { get; set; }
    public string? ShowId { get; set; }
    public string? EpisodeString { get; set; }
}