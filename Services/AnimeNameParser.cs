using System.Text.RegularExpressions;

namespace Aniki.Services;

public class AnimeNameParser
{
    private readonly AbsoluteEpisodeService _absoluteEpisodeService = new();

    public async Task<ParseResult> ParseAnimeFilename(string filename)
    {
        filename = Regex.Replace(filename, @"\.(mkv|mp4|avi|mov)$", "");

        List<string> patterns = new List<string>
        {
            // Pattern 1: [Group] Anime Name - 01 [attributes]
            @"^\[(?:[^\]]+)\]\s*(.+?)\s*-\s*(\d+)(?:v\d)?(?:\s*\[.+?\])*",

            // Pattern 2: [Group] Anime Name 01 [attributes]
            @"^\[(?:[^\]]+)\]\s*(.+?)(?:\s|_)(S\d+E)?(\d+)(?:v\d)?(?:\s*\[.+?\])*",

            // Pattern 3: Anime Name - 01 [attributes]
            @"^(.+?)\s*-\s*(S\d+E)?(\d+)(?:v\d)?(?:\s*\[.+?\])*",

            // Pattern 4: Anime Name S01E01
            @"^(.+?)\s*S(\d+)E(\d+)",

            // Pattern 5: Anime Name.01
            @"^(.+?)\.(S\d+E)?(\d+)",

            // Pattern 6: Anime Name_01
            @"^(.+?)_(S\d+E)?(\d+)"
        };

        foreach (string pattern in patterns)
        {
            Match match = Regex.Match(filename, pattern);
            if (match.Success)
            {
                string animeName = match.Groups[1].Value.Trim();
                string episodeNumberStr = match.Groups[match.Groups.Count - 1].Value;
                int episodeNumber = int.Parse(episodeNumberStr);

                //Clean up anime name
                animeName = Regex.Replace(animeName, @"[._]", " ").Trim();
                animeName = Regex.Replace(animeName, @"\sS\d+$", "").Trim(); //Remove season suffixes like " S2"

                //Check for absolute episode numbering
                (int season, int relativeEpisode) = await _absoluteEpisodeService.GetSeasonAndEpisodeFromAbsolute(animeName, episodeNumber);

                return new ParseResult
                {
                    AnimeName = animeName,
                    Season = season,
                    EpisodeNumber = relativeEpisode.ToString(),
                    AbsoluteEpisodeNumber = episodeNumber
                };
            }
        }

        return new ParseResult
        {
            AnimeName = filename,
            EpisodeNumber = null
        };
    }
}

public class ParseResult
{
    public required string AnimeName { get; set; }
    public int Season { get; set; } = 1;
    public string? EpisodeNumber { get; set; }
    public int? AbsoluteEpisodeNumber { get; set; }

    public override string ToString()
    {
        if (AbsoluteEpisodeNumber.HasValue)
        {
            return $"Anime: {AnimeName}, Season: {Season}, Episode: {EpisodeNumber} (Absolute: {AbsoluteEpisodeNumber})";
        }
        return $"Anime: {AnimeName}, Episode: {EpisodeNumber}";
    }
}