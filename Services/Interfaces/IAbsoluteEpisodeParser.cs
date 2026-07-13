using Aniki.Services.Parser;
using Aniki.Services.Save;

namespace Aniki.Services.Interfaces;

internal interface IAbsoluteEpisodeParser
{
    public Task<(int season, int part, int relativeEpisode, int? animeId)> GetSeasonAndEpisodeFromAbsolute(string animeTitle,
        int absoluteEpisode, int preferredPart, int? preferredYear = null);

    public Task<int?> GetIdForSeason(string animeTitle, int seasonNumber, int part, int? preferredYear = null, int? seasonHint = null);
    public Task<AnimeSeasonsMap?> GetOrCreateSeasonMap(string animeTitle, int preferredPart, int? preferredYear = null);
    public Task<AbsoluteEpisodeParser.SeasonMapMatch?> ResolveSeasonEntry(string animeTitle, int part, int? preferredYear = null, int? seasonHint = null);
}