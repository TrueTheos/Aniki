using System.Collections.Concurrent;
using Aniki.Services.Anime;
using Aniki.Services.Cache;
using Aniki.Services.Interfaces;

namespace Aniki.Services.Parser;

internal sealed class AbsoluteEpisodeParser : IAbsoluteEpisodeParser
{
    private const int SPECIAL_SEASON_KEY = 0;

    private readonly ISaveService _saveService;
    private readonly IAnimeService _animeService;
    private readonly ConcurrentDictionary<int, AnimeSeasonsMap> _animeIdToMapIndex = new();

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> BuildLocks = new();

    private GenericCacheService<string, AnimeSeasonsMap, AnimeSeasonsMap.AnimeSeasonMapField> AnimeSeasonCache
    {
        get
        {
            field ??= _saveService.GetSeasonCache();

            return field;
        }
    }

    public void ClearIndex() => _animeIdToMapIndex.Clear();

    public AbsoluteEpisodeParser(ISaveService saveService, IAnimeService animeService)
    {
        _saveService = saveService;
        _animeService = animeService;
    }

    public async Task<(int season, int part, int relativeEpisode, int? animeId)> GetSeasonAndEpisodeFromAbsolute(
        string animeTitle, int absoluteEpisode, int preferredPart, int? preferredYear = null)
    {
        AnimeSeasonsMap? seasonMap = await GetOrCreateSeasonMap(animeTitle, preferredPart, preferredYear).ConfigureAwait(false);

        if (seasonMap == null || seasonMap.Seasons.Count == 0)
        {
            return (1, 1, absoluteEpisode, null);
        }

        var orderedEpisodes = seasonMap.Seasons
           .OrderBy(s => s.Key)
           .SelectMany(s => s.Value.OrderBy(kvp => kvp.Key)
           .Select(kvp => (SeasonNumber: s.Key, SeasonData: kvp.Value)));

        int accumulatedEpisodes = 0;

        foreach (var (seasonNumber, seasonData) in orderedEpisodes)
        {
            if (IsSpecialMediaType(seasonData.MediaType)) continue;

            int episodesInPart = seasonData.Episodes;

            if (absoluteEpisode <= accumulatedEpisodes + episodesInPart || episodesInPart == 0)
            {
                return (seasonNumber, seasonData.Part, absoluteEpisode - accumulatedEpisodes,
                    GetIdFromMap(seasonMap, seasonNumber, seasonData.Part));
            }

            accumulatedEpisodes += episodesInPart;
        }

        int lastKnownSeason = seasonMap.Seasons.Keys.Max();
        int lastKnownPart = seasonMap.Seasons[lastKnownSeason].Keys.Max();
        return (lastKnownSeason, lastKnownPart, absoluteEpisode - accumulatedEpisodes,
            GetIdFromMap(seasonMap, lastKnownSeason, lastKnownPart));
    }

    public async Task<SeasonMapMatch?> ResolveSeasonEntry(string animeTitle, int part, int? preferredYear = null,
        int? seasonHint = null)
    {
        try
        {
            (string cleanTitle, int? titleYear) = FilenameParser.SplitTitleYear(animeTitle);
            int? year = preferredYear ?? titleYear;

            (AnimeSeasonsMap? map, int? searchAnchorId) =
                await GetOrCreateSeasonMapInternal(cleanTitle, year, part, seasonHint).ConfigureAwait(false);
            if (map == null) return null;

            SeasonMapMatch? match = CreateSeasonMapMatch(map, part, seasonHint, searchAnchorId);
            if (match != null || searchAnchorId.HasValue || seasonHint.HasValue)
            {
                return match;
            }

            var searchResult = await _animeService.SearchAnimeAsync(cleanTitle).ConfigureAwait(false);
            if (searchResult.Count == 0) return null;

            AnimeDetails bestMatch = PickBestSearchResult(searchResult, part, year, seasonHint);
            return CreateSeasonMapMatch(map, part, seasonHint, bestMatch.Id);
        }
        catch
        {
            return null;
        }
    }

    internal sealed class SeasonMapMatch
    {
        public required AnimeSeasonsMap Map { get; init; }
        public required int Season { get; init; }
        public required int Part { get; init; }
        public required int Id { get; init; }
    }

    public async Task<int?> GetIdForSeason(string animeTitle, int seasonNumber, int part, int? preferredYear = null,
        int? seasonHint = null)
    {
        SeasonMapMatch? match = await ResolveSeasonEntry(animeTitle, part, preferredYear, seasonHint).ConfigureAwait(false);
        return match?.Id;
    }

    public async Task<AnimeSeasonsMap?> GetOrCreateSeasonMap(string animeTitle, int preferredPart, int? preferredYear = null)
    {
        (string cleanTitle, int? titleYear) = FilenameParser.SplitTitleYear(animeTitle);
        int? year = preferredYear ?? titleYear;

        (AnimeSeasonsMap? map, _) = await GetOrCreateSeasonMapInternal(cleanTitle, year, preferredPart, null).ConfigureAwait(false);
        return map;
    }

    private async Task<(AnimeSeasonsMap? Map, int? SearchAnchorId)> GetOrCreateSeasonMapInternal(
        string cleanTitle, int? year, int preferredPart, int? seasonHint)
    {
        try
        {
            EnsureIndexHydrated();

            string cacheKey = FilenameParser.BuildCacheKey(cleanTitle, year);

            AnimeSeasonsMap? cachedMap = AnimeSeasonCache.GetWithoutFetching(cacheKey);
            if (cachedMap != null)
            {
                IndexMap(cachedMap);
                return (cachedMap, null);
            }

            SemaphoreSlim lockObj = BuildLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
            await lockObj.WaitAsync().ConfigureAwait(false);

            try
            {
                cachedMap = AnimeSeasonCache.GetWithoutFetching(cacheKey);
                if (cachedMap != null)
                {
                    IndexMap(cachedMap);
                    return (cachedMap, null);
                }

                var searchResult = await _animeService.SearchAnimeAsync(cleanTitle).ConfigureAwait(false);
                if (searchResult.Count == 0) return (null, null);

                AnimeDetails bestMatch = PickBestSearchResult(searchResult, preferredPart, year, seasonHint);
                int foundAnimeId = bestMatch.Id;

                if (_animeIdToMapIndex.TryGetValue(foundAnimeId, out AnimeSeasonsMap? existingMap))
                {
                    AnimeSeasonCache.Update(cacheKey, existingMap, AnimeSeasonsMap.AnimeSeasonMapField.SeasonData);
                    return (existingMap, foundAnimeId);
                }

                AnimeSeasonsMap? newMap = await BuildSeasonMap(foundAnimeId).ConfigureAwait(false);

                if (newMap?.Seasons is { Count: > 0 })
                {
                    AnimeSeasonCache.Update(cacheKey, newMap, AnimeSeasonsMap.AnimeSeasonMapField.SeasonData);
                    IndexMap(newMap);
                }

                return (newMap, foundAnimeId);
            }
            finally
            {
                lockObj.Release();
            }
        }
        catch
        {
            return (null, null);
        }
    }

    private static SeasonMapMatch? CreateSeasonMapMatch(AnimeSeasonsMap map, int part, int? seasonHint, int? searchAnchorId)
    {
        if (searchAnchorId is { } anchorId && FindById(anchorId, specialOnly: true) is { } special)
            return special;

        if (seasonHint is { } hint &&
            map.Seasons.TryGetValue(hint, out var partsForSeason) &&
            partsForSeason.TryGetValue(part, out SeasonData directMatch))
            return Build(hint, directMatch);

        if (searchAnchorId is { } id && FindById(id, specialOnly: false) is { } matched)
            return matched;

        if (seasonHint is null)
        {
            var partMatches = Flatten(map).Where(x => x.Data.Part == part).ToList();

            if (partMatches.Count == 1)
                return Build(partMatches[0].Season, partMatches[0].Data);
        }

        return null;

        SeasonMapMatch? FindById(int id, bool specialOnly) =>
            Flatten(map)
                .Where(x => x.Data.Id == id && (!specialOnly || IsSpecialMediaType(x.Data.MediaType)))
                .Select(x => Build(x.Season, x.Data))
                .FirstOrDefault();

        SeasonMapMatch Build(int season, SeasonData data) =>
            new() { Map = map, Season = season, Part = data.Part, Id = data.Id };
    }

    private static IEnumerable<(int Season, SeasonData Data)> Flatten(AnimeSeasonsMap map) =>
        map.Seasons.SelectMany(kvp => kvp.Value.Values.Select(data => (kvp.Key, data)));

    private static bool IsSpecialMediaType(MediaType mediaType) =>
        mediaType is MediaType.Movie
                  or MediaType.TV_Special
                  or MediaType.TV_Short
                  or MediaType.Special
                  or MediaType.PV
                  or MediaType.Music
                  or MediaType.CM
                  or MediaType.One_Shot;

    private static int? GetIdFromMap(AnimeSeasonsMap? map, int season, int part)
    {
        if (map == null) return null;

        return map.Seasons.TryGetValue(season, out var parts) &&
               parts.TryGetValue(part, out SeasonData data)
            ? data.Id
            : null;
    }

    private void EnsureIndexHydrated()
    {
        foreach (AnimeSeasonsMap map in AnimeSeasonCache.GetAllCachedData())
        {
            IndexMap(map);
        }
    }

    private static AnimeDetails PickBestSearchResult(List<AnimeDetails> results, int part, int? preferredYear,
        int? seasonHint)
    {
        return results
            .Select((anime, index) => new
            {
                Anime = anime,
                Index = index,
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

        int? extractedSeason = FilenameParser.ExtractSeason(title ?? string.Empty);
        if (extractedSeason == null) return 0;

        return extractedSeason.Value == desiredSeason.Value ? 1000 : -1000;
    }

    private static int ScorePartMatch(string? title, int? desiredPart)
    {
        if (desiredPart == null) return 0;

        int extractedPart = FilenameParser.ExtractPart(title ?? string.Empty);
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
        foreach (SeasonData season in map.Seasons.Values.SelectMany(parts => parts.Values))
        {
            _animeIdToMapIndex.TryAdd(season.Id, map);
        }
    }

    private async Task<AnimeSeasonsMap?> BuildSeasonMap(int animeId)
    {
        try
        {
            AnimeSeasonsMap seasonMap = new();
            HashSet<int> visitedIds = [];

            List<(int id, AnimeDetails details)> seasonChain = new();
            int currentId = animeId;

            while (true)
            {
                if (!visitedIds.Add(currentId)) break;

                AnimeDetails? details = await _animeService.GetFieldsAsync(currentId, fields:
                [
                    AnimeField.AlterTitles, AnimeField.StartDate, AnimeField.MediaType,
                    AnimeField.Title, AnimeField.Episodes, AnimeField.RelatedAnime
                ]).ConfigureAwait(false);
                if (details == null) break;

                seasonChain.Insert(0, (currentId, details));

                RelatedAnime? prequel =
                    details.RelatedAnime?.FirstOrDefault(r => r.Relation is RelatedAnime.RelationType.Prequel or RelatedAnime.RelationType.Summary or RelatedAnime.RelationType.FullStory );
                if (prequel?.Details != null) currentId = prequel.Details.Id;
                else break;
            }

            AnimeDetails? currentDetails = seasonChain.FirstOrDefault(x => x.id == animeId).details ??
                                           seasonChain.Last().details;

            while (currentDetails != null)
            {
                RelatedAnime? sequel =
                    currentDetails.RelatedAnime?.FirstOrDefault(r => r.Relation == RelatedAnime.RelationType.Sequel);
                if (sequel?.Details != null && !visitedIds.Contains(sequel.Details.Id))
                {
                    currentId = sequel.Details.Id;
                    visitedIds.Add(currentId);
                    currentDetails = await _animeService.GetFieldsAsync(currentId, fields:
                    [
                        AnimeField.AlterTitles, AnimeField.StartDate, AnimeField.Title,
                        AnimeField.Episodes, AnimeField.RelatedAnime, AnimeField.MediaType
                    ]).ConfigureAwait(false);
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
            int specialPart   = 0;
            foreach ((int id, AnimeDetails details) in seasonChain)
            {
                string title       = details.Title ?? string.Empty;
                int    part        = FilenameParser.ExtractPart(title);
                int?   titleSeason = FilenameParser.ExtractSeason(title);

                int seasonKey;
                int partKey;

                if (IsSpecialMediaType(details.MediaType))
                {
                    seasonKey = SPECIAL_SEASON_KEY;
                    partKey   = ++specialPart;
                }
                else
                {
                    if (titleSeason.HasValue)
                        currentSeason = titleSeason.Value;
                    else if (part == 1)
                        currentSeason++;

                    seasonKey = currentSeason;
                    partKey   = part;
                }

                if (!seasonMap.Seasons.TryGetValue(seasonKey, out var parts))
                {
                    parts = new Dictionary<int, SeasonData>();

                    seasonMap.Seasons[seasonKey] = parts;
                }

                parts[partKey] = new SeasonData
                {
                    Episodes  = details.NumEpisodes ?? 0,
                    Id        = id,
                    MediaType = details.MediaType,
                    Part      = part,
                    Title = details.Title ?? ""
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

internal struct SeasonData
{
    public int Id { get; init; }
    public int Episodes { get; init; }
    public MediaType MediaType { get; init; }
    public int Part { get; init; }
    public string Title { get; init; }
}

internal sealed class AnimeSeasonsMap
{
    internal enum AnimeSeasonMapField
    {
        SeasonData
    }

    [CacheField(AnimeSeasonMapField.SeasonData)]
    public Dictionary<int, Dictionary<int, SeasonData>> Seasons { get; set; } = new();
}
