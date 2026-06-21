using System.Text.RegularExpressions;
using Aniki.Services.Interfaces;

namespace Aniki.Services.Parser;

public class AnimeNameParser : IAnimeNameParser
{
    private readonly IAbsoluteEpisodeParser _absoluteEpisodeParser;

    #region Regexes
    
    private static readonly List<Regex> FilenamePatternRegexes =
    [
        new(@"^(?<name>.+?)\s+Season\s*(?<season>\d+)\s+Part\s*(?<part>\d+)\s*-\s*(?<episode>\d+)(?:v\d+)?(?:\s+.+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^(?<name>.+?)\s+(?<season>\d{1,2})(?:st|nd|rd|th)\s*Season\s+Part\s*(?<part>\d+)\s*-\s*(?<episode>\d+)(?:v\d+)?(?:\s+.+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^(?<name>.+?)\s+S(?<season>\d+)\s+Part\s*(?<part>\d+)\s*-\s*(?<episode>\d+)(?:v\d+)?(?:\s+.+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^(?<name>.+?)\s+S(?<season>\d+)\s*-\s*(?<episode>\d+)(?:v\d+)?(?:\s+.+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^(?<name>.+?)\s+(?<season>\d{1,2})(?:st|nd|rd|th)\s*Season\s*-\s*(?<episode>\d+)(?:v\d+)?(?:\s+.+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^(?<name>.+?)\s+Season\s*(?<season>\d+)\s*-\s*(?<episode>\d+)(?:v\d+)?(?:\s+.+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^(?<name>.+?)\s*-\s*S(?<season>\d+)E(?<episode>\d+)(?:v\d+)?(?:\s+.+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^(?<name>.+?)\s+S(?<season>\d+)E(?<episode>\d+)-E?(?<episodeEnd>\d+)(?:v\d+)?(?:\s+.+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^(?<name>.+?)\s+S(?<season>\d+)E(?<episode>\d+)(?:v\d+)?(?:\s+.+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^(?<name>.+?)\s+(?<season>\d{1,2})(?:st|nd|rd|th)\s*Season\s*E(?<episode>\d+)(?:v\d+)?(?:\s+.+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^(?<name>.+?)\s+(?<season>\d{1,2})(?:st|nd|rd|th)\s*Season\s+(?<episode>\d+)(?:v\d+)?(?:\s+.+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^(?<name>.+?)\s*-\s*(?<episode>\d+)(?:v\d+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^(?<name>.+?)\s+(?<episode>\d+)(?:v\d+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^(?<name>.+?)\.(?<episode>\d+)(?:v\d+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase)
    ];

    private static readonly List<Regex> EpisodeRegexes =
    [
        new(@"^(\d{1,3})(?:v\d+)?$"),
        new(@"^Episode\s*(\d+)(?:v\d+)?$"),
        new(@"^Ep\.?\s*(\d+)(?:v\d+)?$"),
        new(@"^E(\d+)(?:v\d+)?$"),
    ];
    
    private static readonly Regex YearInParensRegex = new(@"\((19|20)\d{2}\)", RegexOptions.Compiled);
    
    private static readonly Regex PartRegex = new(@"\bPart\s*(\d+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex[] SeasonPatternRegexes =
    [
        new(@"\bSeason\s*(\d{1,2})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\b(\d{1,2})(?:st|nd|rd|th)\s*Season\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bS(\d{1,2})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];
    
    #endregion

    public AnimeNameParser(IAbsoluteEpisodeParser absoluteEpisodeParser)
    {
        _absoluteEpisodeParser = absoluteEpisodeParser;
    }

    //todo rework/cleanup
    public FolderParseResult ParseReleaseFolder(string folderName)
    {
        string cleaned = folderName.Replace('.', ' ').Replace('_', ' ');
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
 
        (int season, int seasonIndex) = ExtractSeasonWithIndex(cleaned);
        int part = ExtractPart(cleaned);

        string animeName = seasonIndex >= 0
            ? cleaned[..seasonIndex].Trim().TrimEnd('-', ' ')
            : StripReleaseMetadata(cleaned);

        animeName = StripPart(animeName);
        animeName = Regex.Replace(animeName, @"\s+", " ").Trim();

        (string cleanName, int? year) = SplitTitleYear(animeName);
        return new FolderParseResult(cleanName, season, part, year);
    }
 
    public EpisodeInfo? ParseEpisodeFromFilename(string filename, int defaultSeason = 1, int defaultPart = 1)
    {
        string name = CleanFilename(filename);
        
        int? episode = ExtractEpisode(name);
        int? season  = ExtractSeason(name);

        return (!episode.HasValue && episode!.Value > 0) ? null : new EpisodeInfo(season ?? defaultSeason, defaultPart, episode.Value);
    }
    
    //todo rework/cleanup
    public async Task<ParseResult> ParseAnimeFilename(string filename)
    {
        string originalFilename = StripExtension(filename);
        string cleanedFilename = CleanFilename(filename);
        (_, int? fileYear) = SplitTitleYear(cleanedFilename);
        
        foreach (Regex pattern in FilenamePatternRegexes)
        {
            try
            {
                Match match = pattern.Match(cleanedFilename);
                if (!match.Success)
                    continue;
 
                if (!int.TryParse(match.Groups["episode"].Value, out int episodeNumber))
                    continue;
 
                int season = 1;
                
                bool seasonExplicit = match.Groups["season"].Success;
                if (seasonExplicit && !int.TryParse(match.Groups["season"].Value, out season))
                    continue;

                int? seasonHint = seasonExplicit ? season : null;

                int part = ExtractPart(originalFilename);

                string animeName = match.Groups["name"].Value.Trim('-', ' ');
                animeName = Regex.Replace(animeName, @"\s+", " ");
                animeName = Regex.Replace(
                    animeName,
                    @"\s+(?:(?:S|Season)\s*\d+|\d{1,2}(?:st|nd|rd|th)\s*Season)\s*$",
                    "",
                    RegexOptions.IgnoreCase).Trim();
                
                animeName = StripPart(animeName);
                
                (animeName, int? nameYear) = SplitTitleYear(animeName);
                int? year = nameYear ?? fileYear;
 
                //watchout when u uncomment this. One Piece has more than 999 eps and this will fail
                /*if (episodeNumber < 1 || episodeNumber > 999)
                    continue;*/
 
                if (season > 1 || part > 1)
                {
                    AbsoluteEpisodeParser.SeasonMapMatch? seasonMatch = await _absoluteEpisodeParser.ResolveSeasonEntry(animeName, part, year, seasonHint);
                    int resolvedSeason = seasonMatch?.Season ?? season;
                    
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
 
        (string fallbackTitle, int? fallbackYear) = SplitTitleYear(cleanedFilename);
        return new ParseResult
        {
            AnimeName     = fallbackTitle,
            EpisodeNumber = null,
            Year          = fallbackYear ?? fileYear
        };
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

    #region Season
    
    public static int? ExtractSeason(string text)
    {
        Match? match = FindSeasonMatch(text);
        if (match != null && int.TryParse(match.Groups[1].Value, out int season))
            return season;

        return null;
    }
    
    private (int Season, int Index) ExtractSeasonWithIndex(string text)
    {
        Match? match = FindSeasonMatch(text);
        if (match != null && int.TryParse(match.Groups[1].Value, out int season))
            return (season, match.Index);

        return (1, -1);
    }
    
    private static Match? FindSeasonMatch(string text)
    {
        return SeasonPatternRegexes.Select(regex => regex.Match(text)).FirstOrDefault(match => match.Success);
    }

    #endregion
    
    #region Episode

    private int? ExtractEpisode(string text)
    {
        Match? match = EpisodeRegexes.Select(regex => regex.Match(text)).FirstOrDefault(match => match.Success);
        if (match != null && int.TryParse(match.Groups[1].Value, out int episodeNumber))
            return episodeNumber;

        return null;
    }
    
    #endregion
    
    #region Part
    
    public static int ExtractPart(string text)
    {
        Match match = PartRegex.Match(text);
        return match.Success && int.TryParse(match.Groups[1].Value, out int part) ? part : 1;
    }
    
    private static string StripPart(string text)
    {
        Match match = PartRegex.Match(text);
        if (!match.Success)
            return text;

        string clean = text.Remove(match.Index, match.Length);
        return Regex.Replace(clean, @"\s+", " ").Trim().TrimEnd('-', ' ');
    }
    
    #endregion
    
    #region Year
    
    public static (string Title, int? Year) SplitTitleYear(string title)
    {
        Match match = YearInParensRegex.Match(title);
        if (!match.Success || !int.TryParse(match.Value.Trim('(', ')'), out int year))
            return (title.Trim(), null);

        string clean = title.Remove(match.Index, match.Length);
        clean = Regex.Replace(clean, @"\s+", " ").Trim().TrimEnd('-', ' ');
        return (clean, year);
    }
    
    #endregion
    
    public static string BuildCacheKey(string title, int? year) =>
        year.HasValue ? $"{title}|{year.Value}" : title;
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
