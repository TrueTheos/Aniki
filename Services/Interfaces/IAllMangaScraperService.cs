namespace Aniki.Services.Interfaces;

public interface IAllMangaScraperService
{
    public Task<List<AllMangaSearchResult>> SearchAnimeAsync(string query);
    public Task<List<AllManagaEpisode>> GetEpisodesAsync(string animeUrl);
    public Task<string> GetVideoUrlAsync(string episodeUrl);
}