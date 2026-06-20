using System.Text.RegularExpressions;

namespace Aniki.Services.Parser;

public static class AnimeTitleYearParser
{
    private static readonly Regex YearInParensRegex = new(@"\((19|20)\d{2}\)", RegexOptions.Compiled);

    public static (string Title, int? Year) Split(string title)
    {
        Match match = YearInParensRegex.Match(title);
        if (!match.Success || !int.TryParse(match.Value.Trim('(', ')'), out int year))
            return (title.Trim(), null);

        string clean = title.Remove(match.Index, match.Length);
        clean = Regex.Replace(clean, @"\s+", " ").Trim().TrimEnd('-', ' ');
        return (clean, year);
    }

    public static string BuildCacheKey(string title, int? year) =>
        year.HasValue ? $"{title}|{year.Value}" : title;
}
