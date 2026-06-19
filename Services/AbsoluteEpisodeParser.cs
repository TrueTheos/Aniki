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

    public async Task<(int season, int part, int relativeEpisode)> GetSeasonAndEpisodeFromAbsolute(string animeTitle, int absoluteEpisode, int preferredPart, int? preferredYear = null)
    {
        AnimeSeasonsMap? seasonMap = await GetOrCreateSeasonMap(animeTitle, preferredPart, preferredYear);

        if (seasonMap == null || seasonMap.Seasons.Count == 0)
        {
            return (1, 1, absoluteEpisode);
        }

        int accumulatedEpisodes = 0;
        foreach (int seasonNumber in seasonMap.Seasons.Keys.Order())
        {
            foreach (SeasonData seasonData in seasonMap.Seasons[seasonNumber].OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value))
            {
                int episodesInPart = seasonData.Episodes;

                if (absoluteEpisode <= accumulatedEpisodes + episodesInPart || episodesInPart == 0)
                {
                    return (seasonNumber, seasonData.Part, absoluteEpisode - accumulatedEpisodes);
                }

                if (seasonData.MediaType is
                    MediaType.Movie or
                    MediaType.Unknown or
                    MediaType.TV_Special or
                    MediaType.TV_Short) continue;

                accumulatedEpisodes += episodesInPart;
            }
        }

        int lastKnownSeason = seasonMap.Seasons.Keys.Max();
        int lastKnownPart = seasonMap.Seasons[lastKnownSeason].Keys.Max();
        return (lastKnownSeason, lastKnownPart, absoluteEpisode - accumulatedEpisodes);
    }

    public async Task<SeasonMapMatch?> ResolveSeasonEntry(string animeTitle, int part, int? preferredYear = null, int? seasonHint = null)
    {
        try
        {
            (string cleanTitle, int? titleYear) = AnimeTitleYearParser.Split(animeTitle);
            int?   year     = preferredYear ?? titleYear;
            string cacheKey = AnimeTitleYearParser.BuildCacheKey(cleanTitle, year);

            List<AnimeDetails> searchResult = await _animeService.SearchAnimeAsync(cleanTitle);
            if (searchResult.Count == 0) return null;

            AnimeDetails bestMatch = PickBestSearchResult(searchResult, part, year, seasonHint);
            
            int foundAnimeId = bestMatch.Id;

            AnimeSeasonsMap? map;
            if (_animeIdToMapIndex.TryGetValue(foundAnimeId, out AnimeSeasonsMap? existingMap))
            {
                map = existingMap;
                AnimeSeasonCache.Update(cacheKey, map, AnimeSeasonsMap.AnimeSeasonMapField.SeasonData);
            }
            else
            {
                SemaphoreSlim lockObj = BuildLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
                await lockObj.WaitAsync();
                try
                {
                    if (_animeIdToMapIndex.TryGetValue(foundAnimeId, out existingMap))
                    {
                        map = existingMap;
                    }
                    else
                    {
                        map = await BuildSeasonMap(foundAnimeId);
                        if (map?.Seasons != null && map.Seasons.Count > 0)
                            IndexMap(map);
                    }

                    if (map != null)
                        AnimeSeasonCache.Update(cacheKey, map, AnimeSeasonsMap.AnimeSeasonMapField.SeasonData);
                }
                finally
                {
                    lockObj.Release();
                }
            }

            if (map == null) return null;

            var matched = map.Seasons
                             .SelectMany(kvp => kvp.Value.Select(p => new { Season = kvp.Key, Data = p.Value }))
                             .FirstOrDefault(x => x.Data.Id == foundAnimeId);

            if (matched == null) return null;

            return new SeasonMapMatch
                { Map = map, Season = matched.Season, Part = matched.Data.Part, Id = matched.Data.Id };
        }
        catch
        {
            return null;
        }
    }
    
    public class SeasonMapMatch
    {
        public required AnimeSeasonsMap Map { get; init; }
        public required int Season { get; init; }
        public required int Part { get; init; }
        public required int Id { get; init; }
    }

    public async Task<int?> GetIdForSeason(string animeTitle, int seasonNumber, int part, int? preferredYear = null, int? seasonHint = null)
    {
        SeasonMapMatch? match = await ResolveSeasonEntry(animeTitle, part, preferredYear, seasonHint);
        return match?.Id;
    }

    public async Task<AnimeSeasonsMap?> GetOrCreateSeasonMap(string animeTitle, int preferredPart, int? preferredYear = null)
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

                AnimeDetails bestMatch    = PickBestSearchResult(searchResult, preferredPart, year, null);
                int          foundAnimeId = bestMatch.Id;

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

    private static AnimeDetails PickBestSearchResult(List<AnimeDetails> results, int part, int? preferredYear, int? seasonHint)
    {
        if (preferredYear == null && seasonHint == null && part <= 1)
            return results[0];

        return results
               .Select((anime, index) => new
               {
                   Anime       = anime,
                   Index       = index,
                   SeasonScore = ScoreSeasonMatch(anime.Title, seasonHint),
                   PartScore = ScorePartMatch(anime.Title, part),
                   YearScore = ScoreYearMatch(anime.StartDate, preferredYear)
               })
               .OrderByDescending(x => x.SeasonScore)
               .ThenByDescending(x => x.PartScore)
               .ThenByDescending(x => x.YearScore)
               .ThenBy(x => x.Index)
               .First()
               .Anime;
    }
    
    private static int ScoreSeasonMatch(string? title, int? desiredSeason)
    {
        if (desiredSeason == null) return 0;

        int? extractedSeason = AnimeTitleSeasonPartParser.ExtractSeason(title ?? string.Empty);
        if (extractedSeason == null) return 0;  

        return extractedSeason.Value == desiredSeason.Value ? 1000 : -1000;
    }
    
    private static int ScorePartMatch(string? title, int? desiredPart)
    {
        if (desiredPart == null) return 0;

        int extractedPart = AnimeTitleSeasonPartParser.ExtractPart(title ?? string.Empty);
        return extractedPart == desiredPart.Value ? 1000 : 0;
    }

    private static int ScoreYearMatch(string? startDate, int? preferredYear)
    {
        if (preferredYear == null) return 0;

        int? year = ParseStartYear(startDate);
        if (year == null) return 0;

        return Math.Abs(year.Value - preferredYear.Value) switch
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
        foreach (Dictionary<int, SeasonData> parts in map.Seasons.Values)
        {
            foreach (SeasonData season in parts.Values)
            {
                _animeIdToMapIndex.TryAdd(season.Id, map);
            }
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
            
            int currentSeason = 0;
            for (int i = 0; i < seasonChain.Count; i++)
            {
                (int id, AnimeDetails details) = seasonChain[i];
                string title = details.Title ?? string.Empty;
                int part = AnimeTitleSeasonPartParser.ExtractPart(title);
                int? titleSeason = AnimeTitleSeasonPartParser.ExtractSeason(title);

                if (titleSeason.HasValue)
                    currentSeason = titleSeason.Value;
                else if (part == 1)
                    currentSeason++;

                if (!seasonMap.Seasons.TryGetValue(currentSeason, out Dictionary<int, SeasonData>? parts))
                {
                    parts = new Dictionary<int, SeasonData>();
                    seasonMap.Seasons[currentSeason] = parts;
                }

                parts[part] = new SeasonData
                {
                    Episodes = details.NumEpisodes ?? 0,
                    Id = id,
                    MediaType = details.MediaType,
                    Title = details.Title,
                    Part = part
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