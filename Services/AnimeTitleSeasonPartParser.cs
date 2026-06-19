using System.Text.RegularExpressions;

namespace Aniki.Services;

public static class AnimeTitleSeasonPartParser
{
    private static readonly Regex PartRegex = new(@"\bPart\s*(\d+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex[] SeasonPatternRegexes =
    [
        new(@"\bSeason\s*(\d{1,2})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\b(\d{1,2})(?:st|nd|rd|th)\s*Season\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bS(\d{1,2})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    public static int ExtractPart(string text)
    {
        Match match = PartRegex.Match(text);
        return match.Success && int.TryParse(match.Groups[1].Value, out int part) ? part : 1;
    }

    public static int? ExtractSeason(string text)
    {
        Match? match = FindSeasonMatch(text);
        if (match != null && int.TryParse(match.Groups[1].Value, out int season))
            return season;

        return null;
    }

    public static (int Season, int Index) ExtractSeasonWithIndex(string text)
    {
        Match? match = FindSeasonMatch(text);
        if (match != null && int.TryParse(match.Groups[1].Value, out int season))
            return (season, match.Index);

        return (1, -1);
    }

    public static string StripPart(string text)
    {
        Match match = PartRegex.Match(text);
        if (!match.Success)
            return text;

        string clean = text.Remove(match.Index, match.Length);
        return Regex.Replace(clean, @"\s+", " ").Trim().TrimEnd('-', ' ');
    }

    private static Match? FindSeasonMatch(string text)
    {
        foreach (Regex regex in SeasonPatternRegexes)
        {
            Match match = regex.Match(text);
            if (match.Success)
                return match;
        }

        return null;
    }
}
