using System.Text.RegularExpressions;

namespace Aniki.Services.Parser;

public sealed record ParsedName
{
    public required string Name { get; init; }
    public int? Season { get; init; }
    public int Part { get; init; } = 1;
    public int? Episode { get; init; }
    public int? EpisodeEnd { get; init; }
    public int? Year { get; init; }
}

public static class FilenameParser
{
    private static readonly Regex[] SeasonEpisodeRegexes =
    [
        new(@"\bS(?<season>\d{1,2})\s*E(?<episode>\d{1,4})(?:\s*-\s*E?(?<episodeEnd>\d{1,4}))?\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\b(?<season>\d{1,2})x(?<episode>\d{2,3})\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    private static readonly Regex[] SeasonRegexes =
    [
        new(@"\b(?<season>\d{1,2})(?:st|nd|rd|th)\s*Season\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bSeason\s*(?<season>\d{1,2})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bS(?<season>\d{1,2})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    private static readonly Regex[] PartRegexes =
    [
        new(@"\bPart\s*(?<part>\d{1,2})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bCour\s*(?<part>\d{1,2})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    private static readonly Regex[] EpisodeRegexes =
    [
        new(@"\bE(?<episode>\d{1,4})(?:\s*-\s*E?(?<episodeEnd>\d{1,4}))?\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\b(?:Episode|Ep)\.?\s*(?<episode>\d{1,4})(?:\s*-\s*(?<episodeEnd>\d{1,4}))?\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"-\s*(?<episode>\d{1,4})(?:v\d+)?(?:\s*-\s*E?(?<episodeEnd>\d{1,4}))?(?=\s*(?:-\s*\D.*)?$)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\b(?<episode>\d{1,4})(?:v\d+)?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    private static readonly Regex YearRegex =
        new(@"[\(\[]\s*(?<year>(?:19|20)\d{2})\s*[\)\]]", RegexOptions.Compiled);

    private static readonly Regex BracketGroupRegex = new(@"[\(\[][^\)\]]*[\)\]]", RegexOptions.Compiled);

    private static readonly Regex SeparatorRegex = new(@"[._]+", RegexOptions.Compiled);

    private static readonly Regex MultiSpaceRegex = new(@"\s+", RegexOptions.Compiled);

    private static readonly Regex ExtensionRegex =
        new(@"\.(?:mkv|mp4|avi|mov|wmv|flv)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ReleaseMetadataRegex = new(
        @"\b(?:" +
        @"\d{3,4}p|4k|8k|" +
        @"web[\s-]?dl|web[\s-]?rip|webrip|bluray|blu[\s-]?ray|bdrip|brrip|bd|hdtv|dvdrip|dvd|remux|" +
        @"hevc|x264|x265|h[\s.]?264|h[\s.]?265|avc|10bit|8bit|" +
        @"aac(?:\d(?:\.\d)?)?|flac|ac3|eac3|ddp?[\s.]?\d(?:\.\d)?|dts(?:[\s-]?hd)?|opus|" +
        @"hdr|sdr|dual[\s-]?audio|multi[\s-]?subs?|multi[\s-]?audio|" +
        @"uncensored|censored|\d{3,4}x\d{3,4}" +
        @")\b.*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Parses a raw name. When <paramref name="extractEpisode"/> is false only the
    /// title, season, part and year are extracted (used for folder names).
    /// </summary>
    public static ParsedName Parse(string raw, bool extractEpisode = true)
    {
        string text = StripExtension(raw);
        int? year = TakeYear(ref text);
        text = CleanNoise(text);

        int? season = null;
        int? episode = null;
        int? episodeEnd = null;
        List<int> boundaries = [];

        if (extractEpisode && TryMatch(SeasonEpisodeRegexes, text, out Match combo))
        {
            season = ToInt(combo.Groups["season"]);
            episode = ToInt(combo.Groups["episode"]);
            episodeEnd = ToInt(combo.Groups["episodeEnd"]);
            boundaries.Add(combo.Index);
        }

        if (season is null && TryMatch(SeasonRegexes, text, out Match seasonMatch))
        {
            season = ToInt(seasonMatch.Groups["season"]);
            boundaries.Add(seasonMatch.Index);
        }

        int part = 1;
        if (TryMatch(PartRegexes, text, out Match partMatch))
        {
            part = ToInt(partMatch.Groups["part"]) ?? 1;
            boundaries.Add(partMatch.Index);
        }

        if (extractEpisode && episode is null && TryMatch(EpisodeRegexes, text, out Match episodeMatch))
        {
            episode = ToInt(episodeMatch.Groups["episode"]);
            episodeEnd = ToInt(episodeMatch.Groups["episodeEnd"]);
            boundaries.Add(episodeMatch.Index);
        }

        return new ParsedName
        {
            Name = ExtractName(text, boundaries),
            Season = season,
            Part = part,
            Episode = episode,
            EpisodeEnd = episodeEnd,
            Year = year
        };
    }

    public static int? ParseEpisode(string raw)
    {
        string text = StripExtension(raw);
        TakeYear(ref text);
        text = CleanNoise(text);

        if (TryMatch(SeasonEpisodeRegexes, text, out Match combo))
            return ToInt(combo.Groups["episode"]);

        return TryMatch(EpisodeRegexes, text, out Match episode)
            ? ToInt(episode.Groups["episode"])
            : null;
    }

    public static int? ExtractSeason(string text) =>
        TryMatch(SeasonRegexes, text, out Match match) ? ToInt(match.Groups["season"]) : null;

    public static int ExtractPart(string text) =>
        TryMatch(PartRegexes, text, out Match match) ? ToInt(match.Groups["part"]) ?? 1 : 1;

    public static (string Title, int? Year) SplitTitleYear(string title)
    {
        string text = title;
        int? year = TakeYear(ref text);
        return (MultiSpaceRegex.Replace(text, " ").Trim().TrimEnd('-', ' '), year);
    }

    public static string BuildCacheKey(string title, int? year) =>
        year.HasValue ? $"{title}|{year.Value}" : title;

    private static string StripExtension(string filename) => ExtensionRegex.Replace(filename, "");

    private static int? TakeYear(ref string text)
    {
        Match match = YearRegex.Match(text);
        if (!match.Success)
            return null;

        text = text.Remove(match.Index, match.Length);
        return int.Parse(match.Groups["year"].Value);
    }

    private static string CleanNoise(string text)
    {
        text = BracketGroupRegex.Replace(text, " ");
        text = SeparatorRegex.Replace(text, " ");
        text = ReleaseMetadataRegex.Replace(text, "");
        return MultiSpaceRegex.Replace(text, " ").Trim();
    }

    private static string ExtractName(string text, List<int> boundaries)
    {
        int end = boundaries.Count > 0 ? boundaries.Min() : text.Length;
        string name = text[..end];
        name = MultiSpaceRegex.Replace(name, " ").Trim();
        return name.Trim('-', '_', '.', ':', ' ').Trim();
    }

    private static bool TryMatch(Regex[] regexes, string text, out Match match)
    {
        foreach (Regex regex in regexes)
        {
            match = regex.Match(text);
            if (match.Success)
                return true;
        }

        match = Match.Empty;
        return false;
    }

    private static int? ToInt(Group group) =>
        group.Success && int.TryParse(group.Value, out int value) ? value : null;
}
