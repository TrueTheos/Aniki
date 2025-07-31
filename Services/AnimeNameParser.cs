using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Aniki.Services
{
    public class AnimeNameParser
    {
        public static ParseResult ParseAnimeFilename(string filename)
        {
            // Remove file extension
            filename = Regex.Replace(filename, @"\.(mkv|mp4|avi|mov)$", "");

            // Common patterns for anime filenames
            List<string> patterns = new List<string>
            {
                // Pattern 1: [Group] Anime Name - 01 [attributes]
                @"\[(?:[^\]]+)\]\s*(.+?)\s*-\s*(\d+)(?:v\d)?(?:\s*\[.+?\])*",
            
                // Pattern 2: [Group] Anime Name 01 [attributes]
                @"\[(?:[^\]]+)\]\s*(.+?)(?:\s|_)(\d+)(?:v\d)?(?:\s*\[.+?\])*",
            
                // Pattern 3: Anime Name - 01 [attributes]
                @"(.+?)\s*-\s*(\d+)(?:v\d)?(?:\s*\[.+?\])*",
            
                // Pattern 4: Anime Name S01E01
                @"(.+?)\s*S\d+E(\d+)",
            
                // Pattern 5: Anime Name.01
                @"(.+?)\.(\d+)",
            
                // Pattern 6: Anime Name_01
                @"(.+?)_(\d+)"
            };

            foreach (string pattern in patterns)
            {
                Match match = Regex.Match(filename, pattern);
                if (match.Success)
                {
                    string animeName = match.Groups[1].Value.Trim();
                    int episodeNumber = int.Parse(match.Groups[2].Value);

                    animeName = Regex.Replace(animeName, @"[._]", " ").Trim();

                    return new()
                    {
                        AnimeName = animeName,
                        EpisodeNumber = episodeNumber
                    };
                }
            }

            // If no pattern matched, return a basic guess
            // Look for any number that might be an episode
            Match episodeMatch = Regex.Match(filename, @"(?:E|Ep|Episode|#)(\d+)|(\d+)");
            if (episodeMatch.Success)
            {
                string episode = episodeMatch.Groups[1].Success ? episodeMatch.Groups[1].Value : episodeMatch.Groups[2].Value;
                // Try to extract name by removing episode part and common decorators
                string namePart = Regex.Replace(filename, @"(?:E|Ep|Episode|#)\d+|\d+|\[.*?\]|\(.*?\)", "");
                namePart = Regex.Replace(namePart, @"[._]", " ").Trim();

                return new()
                {
                    AnimeName = string.IsNullOrWhiteSpace(namePart) ? "Unknown" : namePart,
                    EpisodeNumber = int.Parse(episode)
                };
            }

            return new()
            {
                AnimeName = "Unknown",
                EpisodeNumber = -1
            };
        }
    }

    public class ParseResult
    {
        public string AnimeName { get; set; }
        public int EpisodeNumber { get; set; }

        public override string ToString()
        {
            return $"Anime: {AnimeName}, Episode: {EpisodeNumber}";
        }
    }
}
