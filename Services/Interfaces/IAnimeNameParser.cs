using Aniki.Services.Parser;

namespace Aniki.Services.Interfaces;

internal interface IAnimeNameParser
{
    Task<ParseResult> ParseFile(string filename);
    Task<FolderParseResult> ParseFolder(string folderName);
    int? ParseEpisode(string filename);
}

internal sealed record FolderParseResult(string AnimeName, int Season, int Part, int? Year, int? AnimeId);
