using System.Text.RegularExpressions;
using Aniki.Services.Interfaces;

namespace Aniki.Services.Parser;

public class AnimeNameParser : IAnimeNameParser
{
    private readonly IAbsoluteEpisodeParser _absoluteEpisodeParser;

    public AnimeNameParser(IAbsoluteEpisodeParser absoluteEpisodeParser)
    {
        _absoluteEpisodeParser = absoluteEpisodeParser;
    }

    public FolderParseResult ParseReleaseFolder(string folderName)
    {
        string cleaned = folderName.Replace('.', ' ').Replace('_', ' ');
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
 
        (int season, int seasonIndex) = AnimeTitleSeasonPartParser.ExtractSeasonWithIndex(cleaned);
        int part = AnimeTitleSeasonPartParser.ExtractPart(cleaned);

        string animeName = seasonIndex >= 0
            ? cleaned[..seasonIndex].Trim().TrimEnd('-', ' ')
            : StripReleaseMetadata(cleaned);

        animeName = AnimeTitleSeasonPartParser.StripPart(animeName);
        animeName = Regex.Replace(animeName, @"\s+", " ").Trim();

        (string cleanName, int? year) = AnimeTitleYearParser.Split(animeName);
        return new FolderParseResult(cleanName, season, part, year);
    }
 
    public EpisodeParseResult? ParseEpisodeFromFilename(string filename, int defaultSeason = 1, int defaultPart = 1)
    {
        string name = StripExtension(filename);
 
        Match sxExMatch = Regex.Match(name, @"^S(\d+)E(\d+)", RegexOptions.IgnoreCase);
        if (sxExMatch.Success &&
            int.TryParse(sxExMatch.Groups[1].Value, out int fileSeason) &&
            int.TryParse(sxExMatch.Groups[2].Value, out int sxExEpisode))
        {
            return ValidEpisode(fileSeason, defaultPart, sxExEpisode);
        }
 
        Match seasonDashMatch = Regex.Match(name, @"^Season\s*(\d+)\s*-\s*(\d+)", RegexOptions.IgnoreCase);
        if (seasonDashMatch.Success &&
            int.TryParse(seasonDashMatch.Groups[1].Value, out fileSeason) &&
            int.TryParse(seasonDashMatch.Groups[2].Value, out int seasonDashEpisode))
        {
            return ValidEpisode(fileSeason, defaultPart, seasonDashEpisode);
        }
 
        Match ordinalSeasonDashMatch = Regex.Match(name, @"^(\d{1,2})(?:st|nd|rd|th)\s*Season\s*-\s*(\d+)", RegexOptions.IgnoreCase);
        if (ordinalSeasonDashMatch.Success &&
            int.TryParse(ordinalSeasonDashMatch.Groups[1].Value, out fileSeason) &&
            int.TryParse(ordinalSeasonDashMatch.Groups[2].Value, out int ordinalSeasonEpisode))
        {
            return ValidEpisode(fileSeason, defaultPart, ordinalSeasonEpisode);
        }
 
        string cleaned = Regex.Replace(name, @"\[[^\]]*\]", "");
        cleaned = Regex.Replace(cleaned, @"[._]", " ");
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
 
        List<(string pattern, bool hasSeason)> patterns =
        [
            (@"^(\d{1,3})(?:v\d+)?$", false),
            (@"^Episode\s*(\d+)(?:v\d+)?$", false),
            (@"^Ep\.?\s*(\d+)(?:v\d+)?$", false),
            (@"^E(\d+)(?:v\d+)?$", false),
            (@"^(\d{1,2})(?:st|nd|rd|th)\s*Season\s*E(\d+)(?:v\d+)?$", true),
        ];
 
        foreach ((string pattern, bool hasSeason) in patterns)
        {
            Match match = Regex.Match(cleaned, pattern, RegexOptions.IgnoreCase);
            if (!match.Success)
                continue;
 
            if (hasSeason)
            {
                if (!int.TryParse(match.Groups[1].Value, out fileSeason) ||
                    !int.TryParse(match.Groups[2].Value, out int episode))
                {
                    continue;
                }
 
                return ValidEpisode(fileSeason, defaultPart, episode);
            }
 
            if (!int.TryParse(match.Groups[1].Value, out int episodeNumber))
                continue;
 
            return ValidEpisode(defaultSeason, defaultPart, episodeNumber);
        }
 
        return null;
    }
    
    public async Task<ParseResult> ParseAnimeFilename(string filename)
    {
        string originalFilename = StripExtension(filename);
        (_, int? fileYear) = AnimeTitleYearParser.Split(originalFilename.Replace('.', ' ').Replace('_', ' '));
        string cleanedFilename = CleanFilename(filename);
 
        List<string> patterns =
        [
            @"^(?<name>.+?)\s+Season\s*(?<season>\d+)\s+Part\s*(?<part>\d+)\s*-\s*(?<episode>\d+)(?:v\d+)?(?:\s+.+)?$",
            @"^(?<name>.+?)\s+(?<season>\d{1,2})(?:st|nd|rd|th)\s*Season\s+Part\s*(?<part>\d+)\s*-\s*(?<episode>\d+)(?:v\d+)?(?:\s+.+)?$",
            @"^(?<name>.+?)\s+S(?<season>\d+)\s+Part\s*(?<part>\d+)\s*-\s*(?<episode>\d+)(?:v\d+)?(?:\s+.+)?$",
            @"^(?<name>.+?)\s+S(?<season>\d+)\s*-\s*(?<episode>\d+)(?:v\d+)?(?:\s+.+)?$",
            @"^(?<name>.+?)\s+(?<season>\d{1,2})(?:st|nd|rd|th)\s*Season\s*-\s*(?<episode>\d+)(?:v\d+)?(?:\s+.+)?$",
            @"^(?<name>.+?)\s+Season\s*(?<season>\d+)\s*-\s*(?<episode>\d+)(?:v\d+)?(?:\s+.+)?$",
            @"^(?<name>.+?)\s*-\s*S(?<season>\d+)E(?<episode>\d+)(?:v\d+)?(?:\s+.+)?$",
            @"^(?<name>.+?)\s+S(?<season>\d+)E(?<episode>\d+)-E?(?<episodeEnd>\d+)(?:v\d+)?(?:\s+.+)?$",
            @"^(?<name>.+?)\s+S(?<season>\d+)E(?<episode>\d+)(?:v\d+)?(?:\s+.+)?$",
            @"^(?<name>.+?)\s+(?<season>\d{1,2})(?:st|nd|rd|th)\s*Season\s*E(?<episode>\d+)(?:v\d+)?(?:\s+.+)?$",
            @"^(?<name>.+?)\s+(?<season>\d{1,2})(?:st|nd|rd|th)\s*Season\s+(?<episode>\d+)(?:v\d+)?(?:\s+.+)?$",
            @"^(?<name>.+?)\s*-\s*(?<episode>\d+)(?:v\d+)?$",
            @"^(?<name>.+?)\s+(?<episode>\d+)(?:v\d+)?$",
            @"^(?<name>.+?)\.(?<episode>\d+)(?:v\d+)?$"
        ];
 
        foreach (string pattern in patterns)
        {
            try
            {
                Match match = Regex.Match(cleanedFilename, pattern, RegexOptions.IgnoreCase);
                if (!match.Success)
                    continue;
 
                if (!int.TryParse(match.Groups["episode"].Value, out int episodeNumber))
                    continue;
 
                int  season         = 1;
                bool seasonExplicit = match.Groups["season"].Success;
                if (seasonExplicit && !int.TryParse(match.Groups["season"].Value, out season))
                    continue;

                int? seasonHint = seasonExplicit ? season : null;

                int part = AnimeTitleSeasonPartParser.ExtractPart(originalFilename);

                string animeName = match.Groups["name"].Value.Trim('-', ' ');
                animeName = Regex.Replace(animeName, @"\s+", " ");
                animeName = Regex.Replace(
                    animeName,
                    @"\s+(?:(?:S|Season)\s*\d+|\d{1,2}(?:st|nd|rd|th)\s*Season)\s*$",
                    "",
                    RegexOptions.IgnoreCase).Trim();
                animeName = AnimeTitleSeasonPartParser.StripPart(animeName);
                (animeName, int? nameYear) = AnimeTitleYearParser.Split(animeName);
                int? year = nameYear ?? fileYear;
 
                //watchout when u uncomment this. One Piece has more than 999 eps and this will fail
                /*if (episodeNumber < 1 || episodeNumber > 999)
                    continue;*/
 
                if (season > 1 || part > 1)
                {
                    AbsoluteEpisodeParser.SeasonMapMatch? seasonMatch = await _absoluteEpisodeParser.ResolveSeasonEntry(animeName, part, year, seasonHint);
                    int             resolvedSeason = seasonMatch?.Season ?? season;
                    
                    return new ParseResult
                    {
                        AnimeName             = animeName,
                        Season                = resolvedSeason,
                        Part                  = part,
                        EpisodeNumber         = episodeNumber.ToString(),
                        AbsoluteEpisodeNumber = episodeNumber,
                        Year                  = year
                    };
                }

                (int finalSeason, int finalPart, int relativeEpisode) =
                    await _absoluteEpisodeParser.GetSeasonAndEpisodeFromAbsolute(animeName, episodeNumber, part, year);
                return new ParseResult
                {
                    AnimeName             = animeName,
                    Season                = finalSeason,
                    Part                  = finalPart,
                    EpisodeNumber         = relativeEpisode.ToString(),
                    AbsoluteEpisodeNumber = episodeNumber,
                    Year                  = year
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{originalFilename}: {ex.Message}");
            }
        }
 
        (string fallbackTitle, int? fallbackYear) = AnimeTitleYearParser.Split(cleanedFilename);
        return new ParseResult
        {
            AnimeName     = fallbackTitle,
            EpisodeNumber = null,
            Year          = fallbackYear ?? fileYear
        };
    }
 
    private EpisodeParseResult? ValidEpisode(int season, int part, int episodeNumber)
    {
        if (episodeNumber < 1 || episodeNumber > 999)
            return null;

        return new EpisodeParseResult(season, part, episodeNumber);
    }
 
    private string StripReleaseMetadata(string text) =>
        Regex.Replace(text,
            @"\s+(?:1080p|720p|2160p|4K|WEBRip|BluRay|BDRip|HDTV|HEVC|x265|x264|Dual Audio|AAC|WEB-DL|HDR).*$",
            "",
            RegexOptions.IgnoreCase).Trim();
 
    private string StripExtension(string filename) =>
        Regex.Replace(filename, @"\.(mkv|mp4|avi|mov|wmv|flv)$", "", RegexOptions.IgnoreCase);
 
    private string CleanFilename(string filename)
    {
        string cleaned = StripExtension(filename);
        cleaned = Regex.Replace(cleaned, @"\[[^\]]*\]", "");
        cleaned = Regex.Replace(cleaned, @"\([^\)]*\)", "");
        cleaned = Regex.Replace(cleaned, @"[._]", " ");
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
        cleaned = StripReleaseMetadata(cleaned);
        return cleaned;
    }
}

public class ParseResult
{
    public required string AnimeName { get; set; }
    public int Season { get; set; } = 1;
    public int Part { get; set; } = 1;
    public string? EpisodeNumber { get; set; }
    public int? AbsoluteEpisodeNumber { get; set; }
    public int? Year { get; set; }

    public override string ToString()
    {
        if (AbsoluteEpisodeNumber.HasValue)
        {
            return $"Anime: {AnimeName}, Season: {Season}, Episode: {EpisodeNumber} (Absolute: {AbsoluteEpisodeNumber})";
        }
        return $"Anime: {AnimeName}, Episode: {EpisodeNumber}";
    }
}
