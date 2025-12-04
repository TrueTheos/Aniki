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
    
    public async Task<ParseResult> ParseAnimeFilename(string filename)
    {
        filename = Regex.Replace(filename, @"\.(mkv|mp4|avi|mov)$", "");
        
        string originalFilename = filename;
        
        string cleanedFilename = Regex.Replace(filename, @"\[[^\]]*\]", "");
        cleanedFilename = Regex.Replace(cleanedFilename, @"\([^\)]*\)", "");
        
        cleanedFilename = Regex.Replace(cleanedFilename, @"[._]", " ");
        cleanedFilename = Regex.Replace(cleanedFilename, @"\s+", " ").Trim();

        List<string> patterns = new()
        {
            @"^(.+?)\s+S(\d+)\s*-\s*(\d+)(?:v\d+)?$",
            @"^(.+?)\s+Season\s*(\d+)\s*-\s*(\d+)(?:v\d+)?$",
            @"^(.+?)\s*-\s*S(\d+)E(\d+)(?:v\d+)?$",
            @"^(.+?)\s+S(\d+)E(\d+)(?:v\d+)?$",
            @"^(.+?)\s*-\s*(\d{1,3})(?:v\d+)?$",
            @"^(.+?)\s+(\d{1,3})(?:v\d+)?$",
            @"^(.+?)\.(\d{1,3})(?:v\d+)?$"
        };

        foreach (string pattern in patterns)
        {
            try
            {
                Match match = Regex.Match(cleanedFilename, pattern);
                if (match.Success)
                {
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
                    {
                        continue;
                    }
                    
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
                    else
                    {
                        (int finalSeason, int relativeEpisode) = await _absoluteEpisodeParser.GetSeasonAndEpisodeFromAbsolute(animeName, episodeNumber);
                        return new ParseResult
                        {
                            AnimeName = animeName,
                            Season = finalSeason,
                            EpisodeNumber = relativeEpisode.ToString(),
                            AbsoluteEpisodeNumber = episodeNumber,
                            FileName = originalFilename
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"{originalFilename}: {ex.Message}");
            }
        }

        return new ParseResult
        {
            AnimeName = cleanedFilename,
            EpisodeNumber = null,
            FileName = originalFilename
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