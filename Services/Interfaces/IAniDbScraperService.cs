namespace Aniki.Services.Interfaces;

internal interface IAniDbScraperService
{
    public Task<List<AniDbSearchResult>> SearchAnimeAsync(string query);
    public Task<List<AniDbEpisode>> GetEpisodesAsync(string animeUrl);
    public Task<string> GetVideoUrlAsync(string episodeUrl);
}