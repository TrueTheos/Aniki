namespace Aniki.Services.Interfaces;

public interface IAbsoluteEpisodeParser
{
    public Task<(int season, int relativeEpisode)> GetSeasonAndEpisodeFromAbsolute(string animeTitle,
        int absoluteEpisode);

    public Task<int?> GetMalIdForSeason(string animeTitle, int seasonNumber);
    public Task<Dictionary<int, SeasonData>?> GetOrCreateSeasonMap(string animeTitle);
}