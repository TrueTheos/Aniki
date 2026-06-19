using Aniki.Services.Save;

namespace Aniki.Services.Interfaces;

public interface IAbsoluteEpisodeParser
{
    public Task<(int season, int part, int relativeEpisode)> GetSeasonAndEpisodeFromAbsolute(string animeTitle,
        int absoluteEpisode, int? preferredYear = null);

    public Task<int?> GetIdForSeason(string animeTitle, int seasonNumber, int? preferredYear = null, int part = 1);
    public Task<AnimeSeasonsMap?> GetOrCreateSeasonMap(string animeTitle, int? preferredYear = null);
}