using System.Text.RegularExpressions;
using Aniki.Services.Interfaces;

namespace Aniki.Services;

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

        int season = 1;
        int seasonIndex = -1;

        Match seasonMatch = Regex.Match(cleaned, @"\bSeason\s*(\d{1,2})\b", RegexOptions.IgnoreCase);
        if (!seasonMatch.Success)
            seasonMatch = Regex.Match(cleaned, @"\bS(\d{1,2})\b", RegexOptions.IgnoreCase);

        if (seasonMatch.Success && int.TryParse(seasonMatch.Groups[1].Value, out int parsedSeason))
        {
            season = parsedSeason;
            seasonIndex = seasonMatch.Index;
        }

        string animeName = seasonIndex >= 0
            ? cleaned[..seasonIndex].Trim().TrimEnd('-', ' ')
            : StripReleaseMetadata(cleaned);

        animeName = Regex.Replace(animeName, @"\s+", " ").Trim();
        return new FolderParseResult(animeName, season);
    }

    public EpisodeParseResult? ParseEpisodeFromFilename(string filename, int defaultSeason = 1)
    {
        string name = StripExtension(filename);

        Match sxExMatch = Regex.Match(name, @"^S(\d+)E(\d+)", RegexOptions.IgnoreCase);
        if (sxExMatch.Success &&
            int.TryParse(sxExMatch.Groups[1].Value, out int fileSeason) &&
            int.TryParse(sxExMatch.Groups[2].Value, out int sxExEpisode))
        {
            return ValidEpisode(fileSeason, sxExEpisode);
        }

        Match seasonDashMatch = Regex.Match(name, @"^Season\s*(\d+)\s*-\s*(\d+)", RegexOptions.IgnoreCase);
        if (seasonDashMatch.Success &&
            int.TryParse(seasonDashMatch.Groups[1].Value, out fileSeason) &&
            int.TryParse(seasonDashMatch.Groups[2].Value, out int seasonDashEpisode))
        {
            return ValidEpisode(fileSeason, seasonDashEpisode);
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

                return ValidEpisode(fileSeason, episode);
            }

            if (!int.TryParse(match.Groups[1].Value, out int episodeNumber))
                continue;

            return ValidEpisode(defaultSeason, episodeNumber);
        }

        return null;
    }

    public async Task<ParseResult> ParseAnimeFilename(string filename)
    {
        string originalFilename = StripExtension(filename);
        string cleanedFilename = CleanFilename(filename);

        List<string> patterns =
        [
            @"^(.+?)\s+S(\d+)\s*-\s*(\d+)(?:v\d+)?$",
            @"^(.+?)\s+Season\s*(\d+)\s*-\s*(\d+)(?:v\d+)?$",
            @"^(.+?)\s*-\s*S(\d+)E(\d+)(?:v\d+)?$",
            @"^(.+?)\s+S(\d+)E(\d+)(?:v\d+)?$",
            @"^(.+?)\s*-\s*(\d{1,3})(?:v\d+)?$",
            @"^(.+?)\s+(\d{1,3})(?:v\d+)?$",
            @"^(.+?)\.(\d{1,3})(?:v\d+)?$"
        ];

        foreach (string pattern in patterns)
        {
            try
            {
                Match match = Regex.Match(cleanedFilename, pattern);
                if (!match.Success)
                    continue;

                string animeName;
                int season;
                int episodeNumber;

                if (pattern.Contains(@"S(\d+)E(\d+)"))
                {
                    animeName = match.Groups[1].Value.Trim();
                    if (!int.TryParse(match.Groups[2].Value, out season) ||
                        !int.TryParse(match.Groups[3].Value, out episodeNumber))
                    {
                        continue;
                    }
                }
                else if (pattern.Contains(@"S(\d+)\s*-\s*(\d+)"))
                {
                    animeName = match.Groups[1].Value.Trim();
                    if (!int.TryParse(match.Groups[2].Value, out season) ||
                        !int.TryParse(match.Groups[3].Value, out episodeNumber))
                    {
                        continue;
                    }
                }
                else if (pattern.Contains(@"Season\s*(\d+)"))
                {
                    animeName = match.Groups[1].Value.Trim();
                    if (!int.TryParse(match.Groups[2].Value, out season) ||
                        !int.TryParse(match.Groups[3].Value, out episodeNumber))
                    {
                        continue;
                    }
                }
                else
                {
                    animeName = match.Groups[1].Value.Trim();
                    if (!int.TryParse(match.Groups[2].Value, out episodeNumber))
                    {
                        continue;
                    }
                    season = 1;
                }

                animeName = animeName.Trim('-', ' ');
                animeName = Regex.Replace(animeName, @"\s+", " ");
                animeName = Regex.Replace(animeName, @"\s+(S|Season)\s*\d+\s*$", "", RegexOptions.IgnoreCase).Trim();

                if (episodeNumber < 1 || episodeNumber > 999)
                    continue;

                if (season > 1)
                {
                    await _absoluteEpisodeParser.GetOrCreateSeasonMap(animeName);
                    return new ParseResult
                    {
                        AnimeName = animeName,
                        Season = season,
                        EpisodeNumber = episodeNumber.ToString(),
                        AbsoluteEpisodeNumber = episodeNumber,
                        FileName = originalFilename
                    };
                }

                (int finalSeason, int relativeEpisode) =
                    await _absoluteEpisodeParser.GetSeasonAndEpisodeFromAbsolute(animeName, episodeNumber);
                return new ParseResult
                {
                    AnimeName = animeName,
                    Season = finalSeason,
                    EpisodeNumber = relativeEpisode.ToString(),
                    AbsoluteEpisodeNumber = episodeNumber,
                    FileName = originalFilename
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{originalFilename}: {ex.Message}");
            }
        }

        return new ParseResult
        {
            AnimeName = cleanedFilename,
            EpisodeNumber = null,
            FileName = originalFilename
        };
    }

    private static EpisodeParseResult? ValidEpisode(int season, int episodeNumber)
    {
        if (episodeNumber < 1 || episodeNumber > 999)
            return null;

        return new EpisodeParseResult(season, episodeNumber);
    }

    private static string StripReleaseMetadata(string text) =>
        Regex.Replace(text,
            @"\s+(?:1080p|720p|2160p|4K|WEBRip|BluRay|BDRip|HDTV|HEVC|x265|x264|Dual Audio|AAC|WEB-DL|HDR).*$",
            "",
            RegexOptions.IgnoreCase).Trim();

    private static string StripExtension(string filename) =>
        Regex.Replace(filename, @"\.(mkv|mp4|avi|mov|wmv|flv)$", "", RegexOptions.IgnoreCase);

    private static string CleanFilename(string filename)
    {
        string cleaned = StripExtension(filename);
        cleaned = Regex.Replace(cleaned, @"\[[^\]]*\]", "");
        cleaned = Regex.Replace(cleaned, @"\([^\)]*\)", "");
        cleaned = Regex.Replace(cleaned, @"[._]", " ");
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
        return cleaned;
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
