using Aniki.Services.Save;

namespace Aniki.Services.Interfaces;

public interface IAbsoluteEpisodeParser
{
    public Task<(int season, int relativeEpisode)> GetSeasonAndEpisodeFromAbsolute(string animeTitle,
        int absoluteEpisode, int? preferredYear = null);

    public Task<int?> GetIdForSeason(string animeTitle, int seasonNumber, int? preferredYear = null);
    public Task<AnimeSeasonsMap?> GetOrCreateSeasonMap(string animeTitle, int? preferredYear = null);
}