using System.Text.RegularExpressions;
using Serilog;

namespace Aniki.Services;

public class AnimeNameParser
{
    public async Task<ParseResult> ParseAnimeFilename(string filename)
    {
        // Remove file extension
        filename = Regex.Replace(filename, @"\.(mkv|mp4|avi|mov)$", "");
        
        // Store original filename for result
        string originalFilename = filename;
        
        // Remove everything in brackets [] and parentheses () to clean up the filename
        string cleanedFilename = Regex.Replace(filename, @"\[[^\]]*\]", "");
        cleanedFilename = Regex.Replace(cleanedFilename, @"\([^\)]*\)", "");
        
        // Clean up extra whitespace and replace underscores/dots with spaces
        cleanedFilename = Regex.Replace(cleanedFilename, @"[._]", " ");
        cleanedFilename = Regex.Replace(cleanedFilename, @"\s+", " ").Trim();

        List<string> patterns = new List<string>
        {
            // Pattern 1: Title S# - ## (e.g., "Dr. Stone S4 - 16")
            @"^(.+?)\s+S(\d+)\s*-\s*(\d+)(?:v\d+)?$",
            
            // Pattern 2: Title Season# - ## (e.g., "Fairy Tail - 100 Years Quest Season1 - 01")
            @"^(.+?)\s+Season\s*(\d+)\s*-\s*(\d+)(?:v\d+)?$",
            
            // Pattern 3: Title - S#E## (e.g., "Fairy Tail - 100 Years Quest - S1E01")
            @"^(.+?)\s*-\s*S(\d+)E(\d+)(?:v\d+)?$",
            
            // Pattern 4: Title S#E## (e.g., "Fairy Tail - 100 Years Quest S1E01")
            @"^(.+?)\s+S(\d+)E(\d+)(?:v\d+)?$",
            
            // Pattern 5: Title - ## (most common, e.g., "Fairy Tail - 100 Years Quest - 01")
            // This needs to be more careful to not match numbers in the title
            @"^(.+?)\s*-\s*(\d{1,3})(?:v\d+)?$",
            
            // Pattern 6: Title ## (e.g., "Fairy Tail - 100 Years Quest 01")
            // Look for 1-3 digits at the end, preceded by space
            @"^(.+?)\s+(\d{1,3})(?:v\d+)?$",
            
            // Pattern 7: Title.## (e.g., "Fairy.Tail.-.100.Years.Quest.01")
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
                    int season = 1;
                    int episodeNumber;
                    
                    // Determine which groups contain what based on the pattern
                    if (pattern.Contains(@"S(\d+)E(\d+)")) // Patterns 3, 4
                    {
                        animeName = match.Groups[1].Value.Trim();
                        if (!int.TryParse(match.Groups[2].Value, out season) ||
                            !int.TryParse(match.Groups[3].Value, out episodeNumber))
                        {
                            continue;
                        }
                    }
                    else if (pattern.Contains(@"S(\d+)\s*-\s*(\d+)")) // Pattern 1 - "Title S# - ##"
                    {
                        animeName = match.Groups[1].Value.Trim();
                        if (!int.TryParse(match.Groups[2].Value, out season) ||
                            !int.TryParse(match.Groups[3].Value, out episodeNumber))
                        {
                            continue;
                        }
                    }
                    else if (pattern.Contains(@"Season\s*(\d+)")) // Pattern 2
                    {
                        animeName = match.Groups[1].Value.Trim();
                        if (!int.TryParse(match.Groups[2].Value, out season) ||
                            !int.TryParse(match.Groups[3].Value, out episodeNumber))
                        {
                            continue;
                        }
                    }
                    else // Patterns 5, 6, 7 - just episode number
                    {
                        animeName = match.Groups[1].Value.Trim();
                        if (!int.TryParse(match.Groups[2].Value, out episodeNumber))
                        {
                            continue;
                        }
                        season = 1;
                    }
                    
                    // Additional cleanup of anime name
                    animeName = animeName.Trim('-', ' ');
                    animeName = Regex.Replace(animeName, @"\s+", " ");
                    
                    // Remove trailing season indicators from the anime name if they exist
                    animeName = Regex.Replace(animeName, @"\s+(S|Season)\s*\d+\s*$", "", RegexOptions.IgnoreCase).Trim();
                    
                    // Validate episode number (typically 1-999 for anime)
                    if (episodeNumber < 1 || episodeNumber > 999)
                    {
                        continue;
                    }
                    
                    if (season > 1)
                    {
                        await AbsoluteEpisodeParser.GetOrCreateSeasonMap(animeName);
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
                        (int finalSeason, int relativeEpisode) = await AbsoluteEpisodeParser.GetSeasonAndEpisodeFromAbsolute(animeName, episodeNumber);
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

        // If no pattern matched, return the cleaned filename as anime name
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