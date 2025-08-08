namespace Aniki.Services;

using SeasonCache = Dictionary<string, Dictionary<int, SeasonData>>;

public class AbsoluteEpisodeParser
{
    private static SeasonCache _cache = new();

    public static void Init()
    {
        _cache = SaveService.GetSeasonCache();
    }

    public static async Task<(int season, int relativeEpisode)> GetSeasonAndEpisodeFromAbsolute(string animeTitle, int absoluteEpisode)
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

    public static async Task<int?> GetMalIdForSeason(string animeTitle, int seasonNumber)
    {
        var seasonMap = await GetOrCreateSeasonMap(animeTitle);
        if (seasonMap != null && seasonMap.TryGetValue(seasonNumber, out SeasonData seasonData))
        {
            return seasonData.MalId;
        }
        return null;
    }

    public static async Task<Dictionary<int, SeasonData>?> GetOrCreateSeasonMap(string animeTitle)
    {
        if (_cache.TryGetValue(animeTitle, out var seasonMap))
        {
            return seasonMap;
        }

        var searchResult = await MalUtils.SearchAnimeOrdered(animeTitle);
        if (searchResult.Count == 0) return null;
            
        int animeId = searchResult.First().Anime.Id;

        var newMap = await BuildSeasonMap(animeId);
        if (newMap != null && newMap.Count > 0)
        {
            _cache[animeTitle] = newMap;
            SaveService.SaveSeasonCache(_cache);
        }
            
        return newMap;
    }

    private static async Task<Dictionary<int, SeasonData>?> BuildSeasonMap(int animeId)
    {
        try
        {
            var seasonMap = new Dictionary<int, SeasonData>();
            var visitedIds = new HashSet<int>();
            
            var seasonChain = new List<(int id, AnimeDetails details)>();
            int currentId = animeId;
            
            while (true)
            {
                if (visitedIds.Contains(currentId)) break;
                visitedIds.Add(currentId);
                
                AnimeDetails? details = await MalUtils.GetAnimeDetails(currentId, true);
                if (details == null) break;
                
                seasonChain.Insert(0, (currentId, details));
                
                RelatedAnime? prequel = details.RelatedAnime?.FirstOrDefault(r => r.RelationType == "prequel");
                if (prequel?.Node != null)
                {
                    currentId = prequel.Node.Id;
                }
                else
                {
                    break; 
                }
            }
            
            currentId = animeId;
            AnimeDetails? currentDetails = seasonChain.LastOrDefault(x => x.id == animeId).details;
            
            while (currentDetails != null)
            {
                RelatedAnime? sequel = currentDetails.RelatedAnime?.FirstOrDefault(r => r.RelationType == "sequel");
                if (sequel?.Node != null && !visitedIds.Contains(sequel.Node.Id))
                {
                    currentId = sequel.Node.Id;
                    visitedIds.Add(currentId);
                    currentDetails = await MalUtils.GetAnimeDetails(currentId, true);
                    if (currentDetails != null)
                    {
                        seasonChain.Add((currentId, currentDetails));
                    }
                }
                else
                {
                    break;
                }
            }
            
            for (int i = 0; i < seasonChain.Count; i++)
            {
                (int id, AnimeDetails details) = seasonChain[i];
                seasonMap[i + 1] = new SeasonData 
                { 
                    Episodes = details.NumEpisodes, 
                    MalId = id 
                };
            }
            
            return seasonMap;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error building season map for anime ID {animeId}: {ex.Message}");
            return null;
        }
    }
}