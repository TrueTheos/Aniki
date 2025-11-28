using Aniki.Services.Interfaces;

namespace Aniki.Services;

public class AbsoluteEpisodeParser : IAbsoluteEpisodeParser
{
    private readonly ISaveService _saveService;
    private readonly IMalService _malService;
    
    private GenericCacheService<string, AnimeSeasonsMap, AnimeSeasonsMap.AnimeSeasonMapField> _animeSeasonCache;

    public AbsoluteEpisodeParser(ISaveService saveService, IMalService malService)
    {
        _saveService = saveService;
        _malService = malService;
        
        _animeSeasonCache = _saveService.GetSeasonCache();
    }

    public  async Task<(int season, int relativeEpisode)> GetSeasonAndEpisodeFromAbsolute(string animeTitle, int absoluteEpisode)
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

    public  async Task<int?> GetMalIdForSeason(string animeTitle, int seasonNumber)
    {
        var seasonMap = await GetOrCreateSeasonMap(animeTitle);
        if (seasonMap != null && seasonMap.Seasons.TryGetValue(seasonNumber, out SeasonData seasonData))
        {
            return seasonData.MalId;
        }
        return null;
    }

    public  async Task<AnimeSeasonsMap?> GetOrCreateSeasonMap(string animeTitle)
    {
        var seasonMap = _animeSeasonCache.GetWithoutFetching(animeTitle);
        if (seasonMap != null) return seasonMap;

        var searchResult = await _malService.SearchAnimeOrdered(animeTitle);
        if (searchResult.Count == 0) return null;
            
        int animeId = searchResult.First().Node.Id;

        AnimeSeasonsMap? newMap = await BuildSeasonMap(animeId);
        if (newMap?.Seasons != null && newMap.Seasons.Count > 0)
        {
            _animeSeasonCache.Update(animeTitle, newMap, AnimeSeasonsMap.AnimeSeasonMapField.SeasonData);
        }
            
        return newMap;
    }

    private  async Task<AnimeSeasonsMap?> BuildSeasonMap(int animeId)
    {
        try
        {
            AnimeSeasonsMap seasonMap = new();
            HashSet<int> visitedIds = new();
            
            List<(int id, MalAnimeDetails details)> seasonChain = new();
            int currentId = animeId;
            
            while (true)
            {
                if (!visitedIds.Add(currentId)) break;

                MalAnimeDetails? details = await _malService.GetFieldsAsync(currentId, AnimeField.ALTER_TITLES, AnimeField.START_DATE, AnimeField.TITLE, AnimeField.EPISODES, AnimeField.RELATED_ANIME);
                if (details == null) break;
                
                seasonChain.Insert(0, (currentId, details));
                
                MalRelatedAnime? prequel = details.RelatedAnime?.FirstOrDefault(r => r.RelationType == "prequel");
                if (prequel?.Node != null)
                {
                    currentId = prequel.Node.Id;
                }
                else
                {
                    break; 
                }
            }

            MalAnimeDetails? currentDetails = seasonChain.LastOrDefault(x => x.id == animeId).details;
            
            while (currentDetails != null)
            {
                MalRelatedAnime? sequel = currentDetails.RelatedAnime?.FirstOrDefault(r => r.RelationType == "sequel");
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
                (int id, MalAnimeDetails details) = seasonChain[i];
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