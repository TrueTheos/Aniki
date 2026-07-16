using Aniki.Services.Interfaces;

namespace Aniki.Services.Parser;

internal sealed class AnimeNameParser : IAnimeNameParser
{
    private readonly IAbsoluteEpisodeParser _absoluteEpisodeParser;

    public AnimeNameParser(IAbsoluteEpisodeParser absoluteEpisodeParser)
    {
        _absoluteEpisodeParser = absoluteEpisodeParser;
    }

    public async Task<ParseResult> ParseFile(string filename)
    {
        ParsedName parsed = FilenameParser.Parse(filename, extractEpisode: true);
        return await Resolve(parsed).ConfigureAwait(false);
    }
    public async Task<FolderParseResult> ParseFolder(string folderName)
    {
        ParsedName parsed = FilenameParser.Parse(folderName, extractEpisode: false);
        int season = parsed.Season ?? 1;

        int? animeId = await _absoluteEpisodeParser.GetIdForSeason(
            parsed.Name, season, parsed.Part, parsed.Year, parsed.Season).ConfigureAwait(false);

        return new FolderParseResult(parsed.Name, season, parsed.Part, parsed.Year, animeId);
    }

    public int? ParseEpisode(string filename) => FilenameParser.ParseEpisode(filename);

    private async Task<ParseResult> Resolve(ParsedName parsed)
    {
        if (parsed.Episode is not { } episode)
        {
            AbsoluteEpisodeParser.SeasonMapMatch? match =
                await _absoluteEpisodeParser.ResolveSeasonEntry(parsed.Name, parsed.Part, parsed.Year, parsed.Season).ConfigureAwait(false);

            return new ParseResult
            {
                AnimeName = parsed.Name,
                Season = match?.Season ?? parsed.Season ?? 1,
                Part = parsed.Part,
                Year = parsed.Year,
                AnimeId = match?.Id
            };
        }

        // When the name contains an explicit season or part, the episode number is
        // already relative to that season - just resolve which MAL entry it is
        if (parsed.Season is > 1 || parsed.Part > 1)
        {
            AbsoluteEpisodeParser.SeasonMapMatch? match =
                await _absoluteEpisodeParser.ResolveSeasonEntry(parsed.Name, parsed.Part, parsed.Year, parsed.Season).ConfigureAwait(false);

            return new ParseResult
            {
                AnimeName = parsed.Name,
                Season = match?.Season ?? parsed.Season ?? 1,
                Part = parsed.Part,
                EpisodeNumber = episode,
                AbsoluteEpisodeNumber = episode,
                Year = parsed.Year,
                AnimeId = match?.Id
            };
        }

        // Otherwise treat the number as an absolute episode and map it onto the
        // correct season / part across the whole series
        (int season, int part, int relativeEpisode, int? animeId) =
            await _absoluteEpisodeParser.GetSeasonAndEpisodeFromAbsolute(parsed.Name, episode, parsed.Part, parsed.Year).ConfigureAwait(false);

        animeId ??= await _absoluteEpisodeParser.GetIdForSeason(parsed.Name, season, part, parsed.Year).ConfigureAwait(false);

        return new ParseResult
        {
            AnimeName = parsed.Name,
            Season = season,
            Part = part,
            EpisodeNumber = relativeEpisode,
            AbsoluteEpisodeNumber = episode,
            Year = parsed.Year,
            AnimeId = animeId
        };
    }
}

internal sealed class ParseResult
{
    public required string AnimeName { get; init; }
    public int Season { get; init; } = 1;
    public int Part { get; init; } = 1;
    public int? EpisodeNumber { get; init; }
    public int? AbsoluteEpisodeNumber { get; init; }
    public int? Year { get; init; }
    public int? AnimeId { get; init; }

    public override string ToString() =>
        AbsoluteEpisodeNumber.HasValue
            ? $"Anime: {AnimeName}, Season: {Season}, Episode: {EpisodeNumber} (Absolute: {AbsoluteEpisodeNumber})"
            : $"Anime: {AnimeName}, Episode: {EpisodeNumber}";
}
