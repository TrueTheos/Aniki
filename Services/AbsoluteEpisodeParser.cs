using System.Collections.Concurrent;
using Aniki.Services.Anime;
using Aniki.Services.Interfaces;

namespace Aniki.Services;

public class AbsoluteEpisodeParser : IAbsoluteEpisodeParser
{
    private readonly ISaveService _saveService;
    private readonly IAnimeService _animeService;
    
    private GenericCacheService<string, AnimeSeasonsMap, AnimeSeasonsMap.AnimeSeasonMapField> _animeSeasonCache;
    private static readonly ConcurrentDictionary<int, AnimeSeasonsMap> _malIdToMapIndex = new();

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _buildLocks = new();
    
    public AbsoluteEpisodeParser(ISaveService saveService, IAnimeService animeService)
    {
        _saveService = saveService;
        _animeService = animeService;
        _animeSeasonCache = _saveService.GetSeasonCache();
        
        // todo we might want to populate _idToMapIndex with _animeSeasonCache across restarts, 
        // make the map invalid after like 1 day or something
    }

    public async Task<(int season, int relativeEpisode)> GetSeasonAndEpisodeFromAbsolute(string animeTitle, int absoluteEpisode)
    {
        AnimeSeasonsMap? seasonMap = await GetOrCreateSeasonMap(animeTitle);

        if (seasonMap == null || seasonMap.Seasons.Count == 0)
        {
            return (1, absoluteEpisode);
        }

        int accumulatedEpisodes = 0;
        foreach (var season in seasonMap.Seasons.OrderBy(kvp => kvp.Key))
        {
            int seasonNumber = season.Key;
            int episodesInSeason = season.Value.Episodes;

            if (absoluteEpisode <= accumulatedEpisodes + episodesInSeason || episodesInSeason == 0)
            {
                return (seasonNumber, absoluteEpisode - accumulatedEpisodes);
            }
            accumulatedEpisodes += episodesInSeason;
        }

        int lastKnownSeason = seasonMap.Seasons.Keys.Max();
        return (lastKnownSeason + 1, absoluteEpisode - accumulatedEpisodes);
    }

    public async Task<int?> GetMalIdForSeason(string animeTitle, int seasonNumber)
    {
        var seasonMap = await GetOrCreateSeasonMap(animeTitle);
        if (seasonMap != null && seasonMap.Seasons.TryGetValue(seasonNumber, out SeasonData seasonData))
        {
            return seasonData.MalId;
        }
        return null;
    }

    public async Task<AnimeSeasonsMap?> GetOrCreateSeasonMap(string animeTitle)
    {
        try
        {
            var cachedMap = _animeSeasonCache.GetWithoutFetching(animeTitle);
            if (cachedMap != null)
            {
                IndexMap(cachedMap);
                return cachedMap;
            }
            
            var lockObj = _buildLocks.GetOrAdd(animeTitle, _ => new SemaphoreSlim(1, 1));
            await lockObj.WaitAsync();

            try
            {
                cachedMap = _animeSeasonCache.GetWithoutFetching(animeTitle);
                if (cachedMap != null)
                {
                    IndexMap(cachedMap);
                    return cachedMap;
                }

                var searchResult = await _animeService.SearchAnimeAsync(animeTitle);
                if (searchResult.Count == 0) return null;

                int foundAnimeId = searchResult.First().Details.Id;

                if (_malIdToMapIndex.TryGetValue(foundAnimeId, out var existingMap))
                {
                    _animeSeasonCache.Update(animeTitle, existingMap, AnimeSeasonsMap.AnimeSeasonMapField.SeasonData);
                    return existingMap;
                }

                AnimeSeasonsMap? newMap = await BuildSeasonMap(foundAnimeId);

                if (newMap?.Seasons != null && newMap.Seasons.Count > 0)
                {
                    _animeSeasonCache.Update(animeTitle, newMap, AnimeSeasonsMap.AnimeSeasonMapField.SeasonData);
                    IndexMap(newMap);
                }

                return newMap;
            }
            finally
            {
                lockObj.Release();
            }
        }
        catch
        {
            return null;
        }
    }

    private void IndexMap(AnimeSeasonsMap map)
    {
        if (map.Seasons == null) return;

        foreach (var season in map.Seasons.Values)
        {
            _malIdToMapIndex.TryAdd(season.MalId, map);
        }
    }

    private async Task<AnimeSeasonsMap?> BuildSeasonMap(int animeId)
    {
        try
        {
            AnimeSeasonsMap seasonMap = new();
            HashSet<int> visitedIds = new();
            
            List<(int id, AnimeDetails details)> seasonChain = new();
            int currentId = animeId;
            
            while (true)
            {
                if (!visitedIds.Add(currentId)) break;

                AnimeDetails? details = await _animeService.GetFieldsAsync(currentId,  fields: [AnimeField.ALTER_TITLES, AnimeField.START_DATE,
                    AnimeField.TITLE, AnimeField.EPISODES, AnimeField.RELATED_ANIME]);
                if (details == null) break;
                
                seasonChain.Insert(0, (currentId, details));
                
                RelatedAnime? prequel = details.RelatedAnime?.FirstOrDefault(r => r.RelationType == "prequel");
                if (prequel?.Details != null) currentId = prequel.Details.Id;
                else break;
            }

            AnimeDetails? currentDetails = seasonChain.FirstOrDefault(x => x.id == animeId).details;
            
            if (currentDetails == null) currentDetails = seasonChain.Last().details; 
            currentId = seasonChain.Last().id;
            
            while (currentDetails != null)
            {
                RelatedAnime? sequel = currentDetails.RelatedAnime?.FirstOrDefault(r => r.RelationType == "sequel");
                if (sequel?.Details != null && !visitedIds.Contains(sequel.Details.Id))
                {
                    currentId = sequel.Details.Id;
                    visitedIds.Add(currentId);
                    currentDetails = await _animeService.GetFieldsAsync(currentId, fields:[AnimeField.ALTER_TITLES, AnimeField.START_DATE, AnimeField.TITLE,
                        AnimeField.EPISODES, AnimeField.RELATED_ANIME]);
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
                seasonMap.Seasons[i + 1] = new SeasonData 
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