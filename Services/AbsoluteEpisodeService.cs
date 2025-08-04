using Aniki.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Aniki.Services
{
    using SeasonCache = Dictionary<string, Dictionary<int, SeasonData>>;

    public class AbsoluteEpisodeService
    {
        private SeasonCache _cache;

        public AbsoluteEpisodeService()
        {
            _cache = SaveService.GetSeasonCache();
        }

        public async Task<(int season, int relativeEpisode)> GetSeasonAndEpisodeFromAbsolute(string animeTitle, int absoluteEpisode)
        {
            var seasonMap = await GetOrCreateSeasonMap(animeTitle);

            if (seasonMap == null || seasonMap.Count == 0)
            {
                return (1, absoluteEpisode);
            }

            int accumulatedEpisodes = 0;
            foreach (var season in seasonMap.OrderBy(kvp => kvp.Key))
            {
                int seasonNumber = season.Key;
                int episodesInSeason = season.Value.Episodes;

                if (absoluteEpisode <= accumulatedEpisodes + episodesInSeason || episodesInSeason == 0)
                {
                    return (seasonNumber, absoluteEpisode - accumulatedEpisodes);
                }
                accumulatedEpisodes += episodesInSeason;
            }

            int lastKnownSeason = seasonMap.Keys.Max();
            return (lastKnownSeason + 1, absoluteEpisode - accumulatedEpisodes);
        }

        public async Task<int?> GetMalIdForSeason(string animeTitle, int seasonNumber)
        {
            var seasonMap = await GetOrCreateSeasonMap(animeTitle);
            if (seasonMap != null && seasonMap.TryGetValue(seasonNumber, out var seasonData))
            {
                return seasonData.MalId;
            }
            return null;
        }

        private async Task<Dictionary<int, SeasonData>> GetOrCreateSeasonMap(string animeTitle)
        {
            if (_cache.TryGetValue(animeTitle, out var seasonMap))
            {
                return seasonMap;
            }

            var searchResult = await MalUtils.SearchAnimeOrdered(animeTitle);
            if (searchResult.Count == 0) return null;
            
            var animeId = searchResult.First().Anime.Id;

            var newMap = await BuildSeasonMap(animeId);
            if (newMap != null && newMap.Count > 0)
            {
                _cache[animeTitle] = newMap;
                SaveService.SaveSeasonCache(_cache);
            }
            
            return newMap;
        }

        private async Task<Dictionary<int, SeasonData>> BuildSeasonMap(int animeId)
        {
            try
            {
                var firstSeasonId = await GetFirstSeasonId(animeId);

                var seasonMap = new Dictionary<int, SeasonData>();
                var currentSeasonNum = 1;
                var currentSeasonId = firstSeasonId;

                while (true)
                {
                    var details = await MalUtils.GetAnimeDetails(currentSeasonId);
                    seasonMap[currentSeasonNum] = new SeasonData { Episodes = details.NumEpisodes, MalId = currentSeasonId };

                    var related = await MalUtils.GetRelatedAnime(currentSeasonId);
                    var sequel = related.FirstOrDefault(r => r.RelationType == "sequel");

                    if (sequel != null)
                    {
                        currentSeasonId = sequel.Node.Id;
                        currentSeasonNum++;
                    }
                    else
                    {
                        break;
                    }
                }
                return seasonMap;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error building season map for anime ID {animeId}: {ex.Message}");
                return null;
            }
        }

        private async Task<int> GetFirstSeasonId(int animeId)
        {
            var currentId = animeId;
            while (true)
            {
                var related = await MalUtils.GetRelatedAnime(currentId);
                var prequel = related.FirstOrDefault(r => r.RelationType == "prequel");
                if (prequel != null)
                {
                    currentId = prequel.Node.Id;
                }
                else
                {
                    return currentId;
                }
            }
        }
    }
}
