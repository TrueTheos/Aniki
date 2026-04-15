namespace Aniki.Services.Interfaces;

public interface IAllMangaScraperService
{
    public Task<List<AllMangaSearchResult>> SearchAnimeAsync(string query);
    public Task<List<AllMangaEpisode>> GetEpisodesAsync(string animeUrl);
    public Task<string> GetVideoUrlAsync(string episodeUrl);
}