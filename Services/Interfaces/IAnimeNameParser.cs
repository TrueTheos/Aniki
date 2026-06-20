using Aniki.Services.Parser;

namespace Aniki.Services.Interfaces;

public interface IAnimeNameParser
{
    Task<ParseResult> ParseAnimeFilename(string filename);
    FolderParseResult ParseReleaseFolder(string folderName);
    EpisodeInfo? ParseEpisodeFromFilename(string filename, int defaultSeason = 1, int defaultPart = 1);
}

public record FolderParseResult(string AnimeName, int Season, int Part = 1, int? Year = null);

public record EpisodeInfo(int Season, int Part, int EpisodeNumber);
