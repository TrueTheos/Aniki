namespace Aniki.Models;

internal sealed class AniDbSearchResult
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Episodes { get; set; }
    public string Url { get; set; } = string.Empty;
    public int? MalId { get; set; }
    public string? Banner { get; set; }
}

internal sealed class AniDbEpisode
{
    public string Id { get; set; } = string.Empty;
    public int Number { get; set; }
    public string Url { get; set; } = string.Empty;
    public string ShowId { get; set; } = string.Empty;
    public string EpisodeString { get; set; } = string.Empty;
    public int TotalEpisodes { get; set; }
}