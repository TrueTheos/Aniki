namespace Aniki.Services.Interfaces;

public interface IAnimeNameParser
{
    Task<ParseResult> ParseAnimeFilename(string filename);
    FolderParseResult ParseReleaseFolder(string folderName);
    EpisodeParseResult? ParseEpisodeFromFilename(string filename, int defaultSeason = 1);
}

public record FolderParseResult(string AnimeName, int Season);

public record EpisodeParseResult(int Season, int EpisodeNumber);
