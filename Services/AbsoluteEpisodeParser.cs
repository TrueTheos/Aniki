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

    public GenericCacheService<string, AnimeSeasonsMap, AnimeSeasonsMap.AnimeSeasonMapField> AnimeSeasonCache
    {
        get
        {
            if (field == null)
            {
                field = _saveService.GetSeasonCache();
            }
            return field;
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

    public async Task<(int season, int relativeEpisode)> GetSeasonAndEpisodeFromAbsolute(string animeTitle, int absoluteEpisode, int? preferredYear = null)
    {
        AnimeSeasonsMap? seasonMap = await GetOrCreateSeasonMap(animeTitle, preferredYear);

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

    public async Task<int?> GetIdForSeason(string animeTitle, int seasonNumber, int? preferredYear = null)
    {
        AnimeSeasonsMap? seasonMap = await GetOrCreateSeasonMap(animeTitle, preferredYear);
        if (seasonMap != null && seasonMap.Seasons.TryGetValue(seasonNumber, out SeasonData seasonData))
        {
            return seasonData.Id;
        }
        return null;
    }

    public async Task<AnimeSeasonsMap?> GetOrCreateSeasonMap(string animeTitle, int? preferredYear = null)
    {
        try
        {
            (string cleanTitle, int? titleYear) = AnimeTitleYearParser.Split(animeTitle);
            int? year = preferredYear ?? titleYear;
            string cacheKey = AnimeTitleYearParser.BuildCacheKey(cleanTitle, year);

            AnimeSeasonsMap? cachedMap = AnimeSeasonCache.GetWithoutFetching(cacheKey);
            if (cachedMap != null)
            {
                IndexMap(cachedMap);
                return cachedMap;
            }
            
            SemaphoreSlim lockObj = BuildLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
            await lockObj.WaitAsync();

            try
            {
                cachedMap = AnimeSeasonCache.GetWithoutFetching(cacheKey);
                if (cachedMap != null)
                {
                    IndexMap(cachedMap);
                    return cachedMap;
                }

                List<AnimeDetails> searchResult = await _animeService.SearchAnimeAsync(cleanTitle);
                if (searchResult.Count == 0) return null;

                AnimeDetails bestMatch = PickBestSearchResult(searchResult, year);
                int foundAnimeId = bestMatch.Id;

                if (_animeIdToMapIndex.TryGetValue(foundAnimeId, out AnimeSeasonsMap? existingMap))
                {
                    AnimeSeasonCache.Update(cacheKey, existingMap, AnimeSeasonsMap.AnimeSeasonMapField.SeasonData);
                    return existingMap;
                }

                AnimeSeasonsMap? newMap = await BuildSeasonMap(foundAnimeId);

                if (newMap?.Seasons != null && newMap.Seasons.Count > 0)
                {
                    AnimeSeasonCache.Update(cacheKey, newMap, AnimeSeasonsMap.AnimeSeasonMapField.SeasonData);
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

    private static AnimeDetails PickBestSearchResult(List<AnimeDetails> results, int? preferredYear)
    {
        if (preferredYear == null)
            return results[0];

        return results
            .Select((anime, index) => new { Anime = anime, Index = index, YearScore = ScoreYearMatch(anime.StartDate, preferredYear.Value) })
            .OrderByDescending(x => x.YearScore)
            .ThenBy(x => x.Index)
            .First()
            .Anime;
    }

    private static int ScoreYearMatch(string? startDate, int preferredYear)
    {
        int? year = ParseStartYear(startDate);
        if (year == null)
            return 0;

        return Math.Abs(year.Value - preferredYear) switch
        {
            0 => 1000,
            1 => 500,
            2 => 100,
            _ => 0
        };
    }

    private static int? ParseStartYear(string? startDate)
    {
        if (string.IsNullOrWhiteSpace(startDate) || startDate.Length < 4)
            return null;

        return int.TryParse(startDate[..4], out int year) ? year : null;
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
            Console.WriteLine($"Error building season map for anime ID {animeId}: {ex.Message}");
            return null;
        }
    }
}