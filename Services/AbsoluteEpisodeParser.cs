using System.Collections.Concurrent;
using Aniki.Services.Anime;
using Aniki.Services.Cache;
using Aniki.Services.Interfaces;
using Aniki.Services.Save;

namespace Aniki.Services;

public class AbsoluteEpisodeParser : IAbsoluteEpisodeParser
{
    private readonly ISaveService _saveService;
    private readonly IAnimeService _animeService;

    private GenericCacheService<string, AnimeSeasonsMap, AnimeSeasonsMap.AnimeSeasonMapField>? _seasonCache;
    public GenericCacheService<string, AnimeSeasonsMap, AnimeSeasonsMap.AnimeSeasonMapField> AnimeSeasonCache
    {
        get
        {
            if (_seasonCache == null)
            {
                _seasonCache = _saveService.GetSeasonCache();
            }
            return _seasonCache;
        }
    }
    private readonly ConcurrentDictionary<int, AnimeSeasonsMap> _animeIdToMapIndex = new();

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> BuildLocks = new();
    
    public AbsoluteEpisodeParser(ISaveService saveService, IAnimeService animeService)
    {
        _saveService = saveService;
        _animeService = animeService;
        
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
        foreach (KeyValuePair<int, SeasonData> season in seasonMap.Seasons.OrderBy(kvp => kvp.Key))
        {
            int seasonNumber = season.Key;
            int episodesInSeason = season.Value.Episodes;

            if (absoluteEpisode <= accumulatedEpisodes + episodesInSeason || episodesInSeason == 0)
            {
                return (seasonNumber, absoluteEpisode - accumulatedEpisodes);
            }
            
            if(season.Value.MediaType is
               MediaType.Movie or
               MediaType.Unknown or
               MediaType.TV_Special or
               MediaType.TV_Short) continue;
            
            accumulatedEpisodes += episodesInSeason;
        }

        int lastKnownSeason = seasonMap.Seasons.Keys.Max();
        return (lastKnownSeason + 1, absoluteEpisode - accumulatedEpisodes);
    }

    public async Task<int?> GetIdForSeason(string animeTitle, int seasonNumber)
    {
        AnimeSeasonsMap? seasonMap = await GetOrCreateSeasonMap(animeTitle);
        if (seasonMap != null && seasonMap.Seasons.TryGetValue(seasonNumber, out SeasonData seasonData))
        {
            return seasonData.Id;
        }
        return null;
    }

    public async Task<AnimeSeasonsMap?> GetOrCreateSeasonMap(string animeTitle)
    {
        try
        {
            AnimeSeasonsMap? cachedMap = AnimeSeasonCache.GetWithoutFetching(animeTitle);
            if (cachedMap != null)
            {
                IndexMap(cachedMap);
                return cachedMap;
            }
            
            SemaphoreSlim lockObj = BuildLocks.GetOrAdd(animeTitle, _ => new SemaphoreSlim(1, 1));
            await lockObj.WaitAsync();

            try
            {
                cachedMap = AnimeSeasonCache.GetWithoutFetching(animeTitle);
                if (cachedMap != null)
                {
                    IndexMap(cachedMap);
                    return cachedMap;
                }

                List<AnimeDetails> searchResult = await _animeService.SearchAnimeAsync(animeTitle);
                if (searchResult.Count == 0) return null;

                int foundAnimeId = searchResult.First().Id;

                if (_animeIdToMapIndex.TryGetValue(foundAnimeId, out AnimeSeasonsMap? existingMap))
                {
                    AnimeSeasonCache.Update(animeTitle, existingMap, AnimeSeasonsMap.AnimeSeasonMapField.SeasonData);
                    return existingMap;
                }

                AnimeSeasonsMap? newMap = await BuildSeasonMap(foundAnimeId);

                if (newMap?.Seasons != null && newMap.Seasons.Count > 0)
                {
                    AnimeSeasonCache.Update(animeTitle, newMap, AnimeSeasonsMap.AnimeSeasonMapField.SeasonData);
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
        foreach (SeasonData season in map.Seasons.Values)
        {
            _animeIdToMapIndex.TryAdd(season.Id, map);
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

                AnimeDetails? details = await _animeService.GetFieldsAsync(currentId,  fields: [AnimeField.AlterTitles, AnimeField.StartDate,
                    AnimeField.Title, AnimeField.Episodes, AnimeField.RelatedAnime]);
                if (details == null) break;
                
                seasonChain.Insert(0, (currentId, details));
                
                RelatedAnime? prequel = details.RelatedAnime?.FirstOrDefault(r => r.Relation == RelatedAnime.RelationType.Prequel);
                if (prequel?.Details != null) currentId = prequel.Details.Id;
                else break;
            }

            AnimeDetails? currentDetails = seasonChain.FirstOrDefault(x => x.id == animeId).details;
            
            if (currentDetails == null) currentDetails = seasonChain.Last().details; 
            
            while (currentDetails != null)
            {
                RelatedAnime? sequel = currentDetails.RelatedAnime?.FirstOrDefault(r => r.Relation == RelatedAnime.RelationType.Sequel);
                if (sequel?.Details != null && !visitedIds.Contains(sequel.Details.Id))
                {
                    currentId = sequel.Details.Id;
                    visitedIds.Add(currentId);
                    currentDetails = await _animeService.GetFieldsAsync(currentId, fields: [AnimeField.AlterTitles, AnimeField.StartDate, AnimeField.Title,
                        AnimeField.Episodes, AnimeField.RelatedAnime]);
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
                    Id = id,
                    MediaType = details.MediaType
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