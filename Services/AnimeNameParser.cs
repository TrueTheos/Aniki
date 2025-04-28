using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Aniki.Services
{
    public class AnimeNameParser
    {
        public static ParseResult ParseAnimeFilename(string filename)
        {
            // Remove file extension
            filename = Regex.Replace(filename, @"\.(mkv|mp4|avi|mov)$", "");

            // Common patterns for anime filenames
            var patterns = new List<string>
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

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(filename, pattern);
                if (match.Success)
                {
                    var animeName = match.Groups[1].Value.Trim();
                    var episodeNumber = int.Parse(match.Groups[2].Value);

                    animeName = Regex.Replace(animeName, @"[._]", " ").Trim();

                    return new ParseResult
                    {
                        AnimeName = animeName,
                        EpisodeNumber = episodeNumber
                    };
                }
            }

            // If no pattern matched, return a basic guess
            // Look for any number that might be an episode
            var episodeMatch = Regex.Match(filename, @"(?:E|Ep|Episode|#)(\d+)|(\d+)");
            if (episodeMatch.Success)
            {
                var episode = episodeMatch.Groups[1].Success ? episodeMatch.Groups[1].Value : episodeMatch.Groups[2].Value;
                // Try to extract name by removing episode part and common decorators
                var namePart = Regex.Replace(filename, @"(?:E|Ep|Episode|#)\d+|\d+|\[.*?\]|\(.*?\)", "");
                namePart = Regex.Replace(namePart, @"[._]", " ").Trim();

                return new ParseResult
                {
                    AnimeName = string.IsNullOrWhiteSpace(namePart) ? "Unknown" : namePart,
                    EpisodeNumber = int.Parse(episode)
                };
            }

            return new ParseResult
            {
                AnimeName = "Unknown",
                EpisodeNumber = null
            };
        }
    }

    public class ParseResult
    {
        public string AnimeName { get; set; }
        public int? EpisodeNumber { get; set; }

        public override string ToString()
        {
            return $"Anime: {AnimeName}, Episode: {EpisodeNumber}";
        }
    }
}
