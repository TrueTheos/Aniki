using Aniki.Services.Parser;

namespace Aniki.Services.Interfaces;

public interface IAnimeNameParser
{
    Task<ParseResult> ParseFile(string filename);
    Task<FolderParseResult> ParseFolder(string folderName);
    int? ParseEpisode(string filename);
}

public record FolderParseResult(string AnimeName, int Season, int Part, int? Year, int? AnimeId);
