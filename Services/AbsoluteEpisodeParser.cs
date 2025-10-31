using Aniki.Models.MAL;
using Aniki.Services.Interfaces;

namespace Aniki.Services;

public class AbsoluteEpisodeParser : IAbsoluteEpisodeParser
{
    private readonly ISaveService _saveService;
    private readonly IMalService _malService;
    
    private SeasonCache _cache = new();

    public AbsoluteEpisodeParser(ISaveService saveService, IMalService malService)
    {
        _saveService = saveService;
        _malService = malService;
        
        _cache = _saveService.GetSeasonCache();

    }

    public  async Task<(int season, int relativeEpisode)> GetSeasonAndEpisodeFromAbsolute(string animeTitle, int absoluteEpisode)
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

    public  async Task<int?> GetMalIdForSeason(string animeTitle, int seasonNumber)
    {
        var seasonMap = await GetOrCreateSeasonMap(animeTitle);
        if (seasonMap != null && seasonMap.TryGetValue(seasonNumber, out SeasonData seasonData))
        {
            return seasonData.MalId;
        }
        return null;
    }

    public  async Task<Dictionary<int, SeasonData>?> GetOrCreateSeasonMap(string animeTitle)
    {
        if (_cache.TryGetValue(animeTitle, out var seasonMap))
        {
            return seasonMap;
        }

        var searchResult = await _malService.SearchAnimeOrdered(animeTitle);
        if (searchResult.Count == 0) return null;
            
        int animeId = searchResult.First().Node.Id;

        var newMap = await BuildSeasonMap(animeId);
        if (newMap != null && newMap.Count > 0)
        {
            _cache[animeTitle] = newMap;
            _saveService.SaveSeasonCache(_cache);
        }
            
        return newMap;
    }

    private  async Task<Dictionary<int, SeasonData>?> BuildSeasonMap(int animeId)
    {
        try
        {
            var seasonMap = new Dictionary<int, SeasonData>();
            var visitedIds = new HashSet<int>();
            
            var seasonChain = new List<(int id, AnimeFieldSet details)>();
            int currentId = animeId;
            
            while (true)
            {
                if (visitedIds.Contains(currentId)) break;
                visitedIds.Add(currentId);
                
                AnimeFieldSet? details = await _malService.GetFieldsAsync(currentId, AnimeField.ALTER_TITLES, AnimeField.START_DATE, AnimeField.TITLE, AnimeField.EPISODES, AnimeField.RELATED_ANIME);
                if (details == null) break;
                
                seasonChain.Insert(0, (currentId, details));
                
                MAL_RelatedAnime? prequel = details.RelatedAnime?.FirstOrDefault(r => r.RelationType == "prequel");
                if (prequel?.Node != null)
                {
                    currentId = prequel.Node.Id;
                }
                else
                {
                    break; 
                }
            }

            AnimeFieldSet? currentDetails = seasonChain.LastOrDefault(x => x.id == animeId).details;
            
            while (currentDetails != null)
            {
                MAL_RelatedAnime? sequel = currentDetails.RelatedAnime?.FirstOrDefault(r => r.RelationType == "sequel");
                if (sequel?.Node != null && !visitedIds.Contains(sequel.Node.Id))
                {
                    currentId = sequel.Node.Id;
                    visitedIds.Add(currentId);
                    currentDetails = await _malService.GetFieldsAsync(currentId, AnimeField.ALTER_TITLES, AnimeField.START_DATE, AnimeField.TITLE, AnimeField.EPISODES, AnimeField.RELATED_ANIME);
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
                (int id, AnimeFieldSet details) = seasonChain[i];
                seasonMap[i + 1] = new SeasonData 
                { 
                    Episodes = details.NumEpisodes ?? 0, 
                    MalId = id 
                };
            }
            
            return seasonMap;
        }
        catch (Exception ex)
        {
            Log.Information($"Error building season map for anime ID {animeId}: {ex.Message}");
            return null;
        }
    }
}