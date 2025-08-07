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
            // Pattern 1: [Group] Anime Name S# - ## [attributes]
            @"^\[([^\]]+)\]\s(.+?)\sS(\d+)\s-\s(\d+)",

            // Pattern 2: [Group] Anime Name - 01 [attributes]
            @"^\[(?:[^\]]+)\]\s*(.+?)\s*-\s*(\d+)(?:v\d)?(?:\s*\[.+?\])*",

            // Pattern 3: [Group] Anime Name 01 [attributes]
            @"^\[(?:[^\]]+)\]\s*(.+?)(?:\s|_)(S\d+E)?(\d+)(?:v\d)?(?:\s*\[.+?\])*",

            // Pattern 4: Anime Name - 01 [attributes]
            @"^(.+?)\s*-\s*(S\d+E)?(\d+)(?:v\d)?(?:\s*\[.+?\])*",

            // Pattern 5: Anime Name S01E01
            @"^(.+?)\s*S(\d+)E(\d+)",

            // Pattern 6: Anime Name.01
            @"^(.+?)\.(S\d+E)?(\d+)",

            // Pattern 7: Anime Name_01
            @"^(.+?)_(S\d+E)?(\d+)"
        };

        foreach (string pattern in patterns)
        {
            Match match = Regex.Match(filename, pattern);
            if (match.Success)
            {
                string animeName;
                int season;
                int episodeNumber;

                if (pattern.Contains(@"S(\d+)"))
                {
                    animeName = match.Groups[2].Value.Trim();
                    season = int.Parse(match.Groups[3].Value);
                    episodeNumber = int.Parse(match.Groups[4].Value);
                }
                else
                {
                    animeName = match.Groups[1].Value.Trim();
                    string episodeNumberStr = match.Groups[match.Groups.Count - 1].Value;
                    episodeNumber = int.Parse(episodeNumberStr);
                    season = 1; // Default to season 1 if not specified
                }

                //Clean up anime name
                animeName = Regex.Replace(animeName, @"[._]", " ").Trim();
                animeName = Regex.Replace(animeName, @"\sS\d+$", "").Trim(); //Remove season suffixes like " S2"

                //Check for absolute episode numbering
                (int finalSeason, int relativeEpisode) = await _absoluteEpisodeService.GetSeasonAndEpisodeFromAbsolute(animeName, episodeNumber);

                return new ParseResult
                {
                    AnimeName = animeName,
                    Season = season > 1 ? season : finalSeason,
                    EpisodeNumber = relativeEpisode.ToString(),
                    AbsoluteEpisodeNumber = episodeNumber,
                    FileName = filename
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
    public string FileName { get; set; } = "";

    public override string ToString()
    {
        if (AbsoluteEpisodeNumber.HasValue)
        {
            return $"Anime: {AnimeName}, Season: {Season}, Episode: {EpisodeNumber} (Absolute: {AbsoluteEpisodeNumber})";
        }
        return $"Anime: {AnimeName}, Episode: {EpisodeNumber}";
    }
}