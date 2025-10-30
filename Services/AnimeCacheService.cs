using Aniki.Models.MAL;
using System.Collections.Concurrent;
using Aniki.Misc;
using Aniki.Services.Interfaces;
using Avalonia.Media.Imaging;

namespace Aniki.Services;

public enum AnimeField 
{
    TITLE, 
    MAIN_PICTURE, 
    STATUS, 
    SYNOPSIS, 
    ALTER_TITLES, 
    MY_LIST_STATUS, 
    EPISODES,
    POPULARITY, 
    PICTURE, 
    STUDIOS, 
    START_DATE, 
    MEAN, 
    GENRES, 
    RELATED_ANIME, 
    VIDEOS, 
    NUM_FAV, 
    STATS, 
    TRAILER_URL
}

public enum FieldFetchState : byte
{
    NotFetched = 0,
    HasValue = 1,
    NullFetched = 2
}

public sealed class AnimeCacheEntry
{
    public int Id;
    public FieldCacheEntry<string> Title;
    public FieldCacheEntry<MAL_MainPicture> MainPicture;
    public FieldCacheEntry<string> Status;
    public FieldCacheEntry<string> Synopsis;
    public FieldCacheEntry<MAL_AlternativeTitles> AlternativeTitles;
    public FieldCacheEntry<MAL_MyListStatus> MyListStatus;
    public FieldCacheEntry<int> NumEpisodes;
    public FieldCacheEntry<int> Popularity;
    public FieldCacheEntry<Bitmap> Picture;
    public FieldCacheEntry<MAL_Studio[]> Studios;
    public FieldCacheEntry<string> StartDate;
    public FieldCacheEntry<float> Mean;
    public FieldCacheEntry<MAL_Genre[]> Genres;
    public FieldCacheEntry<MAL_RelatedAnime[]> RelatedAnime;
    public FieldCacheEntry<MAL_Video[]> Videos;
    public FieldCacheEntry<int> NumFavorites;
    public FieldCacheEntry<MAL_Statistics> Statistics;
    public FieldCacheEntry<string> TrailerURL;
}

public struct FieldCacheEntry<T>
{
    public T? Value;
    public FieldFetchState State;
    public bool WasFetched;

    public void Set(T? value)
    {
        Value = value;
        WasFetched = true;
        if (value != null) State = FieldFetchState.HasValue;
        else
        {
            State = WasFetched ? FieldFetchState.NullFetched : FieldFetchState.NotFetched;
        }
    }

    public readonly bool HasData => State != FieldFetchState.NotFetched;
}

public class AnimeFieldSet
{
    private readonly HashSet<AnimeField> _requestedFields;
    private readonly int _animeId;
    private readonly AnimeCacheService _cache;

    internal AnimeFieldSet(int animeId, AnimeCacheService cache, AnimeField[] requestedFields)
    {
        _animeId = animeId;
        _cache = cache;
        _requestedFields = new HashSet<AnimeField>(requestedFields);
    }

    private void EnsureFetched(AnimeField field, string propertyName)
    {
        if (!_requestedFields.Contains(field))
        {
            throw new InvalidOperationException(
                $"Field '{propertyName}' was not requested. Requested fields: [{string.Join(", ", _requestedFields)}]");
        }
    }

    public int AnimeId => _animeId;

    public string? Title 
    { 
        get 
        { 
            EnsureFetched(AnimeField.TITLE, nameof(Title)); 
            return _cache.GetTitle(_animeId); 
        } 
    }

    public MAL_MainPicture? MainPicture 
    { 
        get 
        { 
            EnsureFetched(AnimeField.MAIN_PICTURE, nameof(MainPicture)); 
            return _cache.GetMainPicture(_animeId); 
        } 
    }

    public string? Status 
    { 
        get 
        { 
            EnsureFetched(AnimeField.STATUS, nameof(Status)); 
            return _cache.GetStatus(_animeId); 
        } 
    }

    public string? Synopsis 
    { 
        get 
        { 
            EnsureFetched(AnimeField.SYNOPSIS, nameof(Synopsis)); 
            return _cache.GetSynopsis(_animeId); 
        } 
    }

    public MAL_AlternativeTitles? AlternativeTitles 
    { 
        get 
        { 
            EnsureFetched(AnimeField.ALTER_TITLES, nameof(AlternativeTitles)); 
            return _cache.GetAlternativeTitles(_animeId); 
        } 
    }

    public MAL_MyListStatus? MyListStatus 
    { 
        get 
        { 
            EnsureFetched(AnimeField.MY_LIST_STATUS, nameof(MyListStatus)); 
            return _cache.GetMyListStatus(_animeId); 
        } 
    }

    public int? NumEpisodes 
    { 
        get 
        { 
            EnsureFetched(AnimeField.EPISODES, nameof(NumEpisodes)); 
            return _cache.GetNumEpisodes(_animeId); 
        } 
    }

    public int? Popularity 
    { 
        get 
        { 
            EnsureFetched(AnimeField.POPULARITY, nameof(Popularity)); 
            return _cache.GetPopularity(_animeId); 
        } 
    }

    public Bitmap? Picture 
    { 
        get 
        { 
            EnsureFetched(AnimeField.PICTURE, nameof(Picture)); 
            return _cache.GetPicture(_animeId); 
        } 
    }

    public MAL_Studio[]? Studios 
    { 
        get 
        { 
            EnsureFetched(AnimeField.STUDIOS, nameof(Studios)); 
            return _cache.GetStudios(_animeId); 
        } 
    }

    public string? StartDate 
    { 
        get 
        { 
            EnsureFetched(AnimeField.START_DATE, nameof(StartDate)); 
            return _cache.GetStartDate(_animeId); 
        } 
    }

    public float? Mean 
    { 
        get 
        { 
            EnsureFetched(AnimeField.MEAN, nameof(Mean)); 
            return _cache.GetMean(_animeId); 
        } 
    }

    public MAL_Genre[]? Genres 
    { 
        get 
        { 
            EnsureFetched(AnimeField.GENRES, nameof(Genres)); 
            return _cache.GetGenres(_animeId); 
        } 
    }

    public MAL_RelatedAnime[]? RelatedAnime 
    { 
        get 
        { 
            EnsureFetched(AnimeField.RELATED_ANIME, nameof(RelatedAnime)); 
            return _cache.GetRelatedAnime(_animeId); 
        } 
    }

    public MAL_Video[]? Videos 
    { 
        get 
        { 
            EnsureFetched(AnimeField.VIDEOS, nameof(Videos)); 
            return _cache.GetVideos(_animeId); 
        } 
    }

    public int? NumFavorites 
    { 
        get 
        { 
            EnsureFetched(AnimeField.NUM_FAV, nameof(NumFavorites)); 
            return _cache.GetNumFavorites(_animeId); 
        } 
    }

    public MAL_Statistics? Statistics 
    { 
        get 
        { 
            EnsureFetched(AnimeField.STATS, nameof(Statistics)); 
            return _cache.GetStats(_animeId); 
        } 
    }

    public string? TrailerURL 
    { 
        get 
        { 
            EnsureFetched(AnimeField.TRAILER_URL, nameof(TrailerURL)); 
            return _cache.GetTrailerUrl(_animeId); 
        } 
    }

    public bool HasField(AnimeField field) => _requestedFields.Contains(field);

    public IReadOnlySet<AnimeField> RequestedFields => _requestedFields;
    
    public AnimeCardData ToCardData()
    {
        return new AnimeCardData()
        {
            AnimeId = AnimeId,
            Title = Title,
            ImageUrl = MainPicture == null ? null : string.IsNullOrEmpty(MainPicture.Large) ? MainPicture.Medium : MainPicture.Large,
            Score = Mean ?? 0,
            Status = MyListStatus?.Status ?? AnimeStatusApi.none
        };
    }
}

public class AnimeCacheService
{
    private readonly ConcurrentDictionary<int, AnimeCacheEntry> _cache = new();
    private readonly Func<int, AnimeField[], Task<MAL_AnimeDetails?>>? _batchFetchFunc;

    public AnimeCacheService(Func<int, AnimeField[], Task<MAL_AnimeDetails?>>? batchFetchFunc)
    {
        _batchFetchFunc = batchFetchFunc;
    }

    private AnimeCacheEntry GetOrCreateEntry(int animeId)
    {
        return _cache.GetOrAdd(animeId, id => new AnimeCacheEntry { Id = id });
    }

    public void AddOrUpdate(MAL_AnimeDetails anime, params AnimeField[] fields)
    {
        var entry = GetOrCreateEntry(anime.Id);

        entry.Id = anime.Id;
        
        
        foreach (var field in fields)
        {
            switch (field)
            {
                case AnimeField.TITLE: entry.Title.Set(anime.Title); break;
                case AnimeField.MAIN_PICTURE: entry.MainPicture.Set(anime.MainPicture); break;
                case AnimeField.STATUS: entry.Status.Set(anime.Status); break;
                case AnimeField.SYNOPSIS: entry.Synopsis.Set(anime.Synopsis); break;
                case AnimeField.ALTER_TITLES: entry.AlternativeTitles.Set(anime.AlternativeTitles); break;
                case AnimeField.MY_LIST_STATUS: entry.MyListStatus.Set(anime.MyListStatus); break;
                case AnimeField.EPISODES: entry.NumEpisodes.Set(anime.NumEpisodes ?? 0); break;
                case AnimeField.POPULARITY: entry.Popularity.Set(anime.Popularity ?? 0); break;
                case AnimeField.PICTURE: entry.Picture.Set(anime.Picture); break;
                case AnimeField.STUDIOS: entry.Studios.Set(anime.Studios); break;
                case AnimeField.START_DATE: entry.StartDate.Set(anime.StartDate); break;
                case AnimeField.MEAN: entry.Mean.Set(anime.Mean ?? 0); break;
                case AnimeField.GENRES: entry.Genres.Set(anime.Genres); break;
                case AnimeField.RELATED_ANIME: entry.RelatedAnime.Set(anime.RelatedAnime); break;
                case AnimeField.VIDEOS: entry.Videos.Set(anime.Videos); break;
                case AnimeField.NUM_FAV: entry.NumFavorites.Set(anime.NumFavorites ?? 0); break;
                case AnimeField.STATS: entry.Statistics.Set(anime.Statistics); break;
                case AnimeField.TRAILER_URL: entry.TrailerURL.Set(anime.TrailerURL); break;
            };
        }
    }

    private T? GetOrFetchStruct<T>(int animeId, AnimeField field, ref FieldCacheEntry<T> cacheEntry) where T : struct
    {
        return cacheEntry.HasData ? cacheEntry.Value : null;
    }

    private T? GetOrFetchClass<T>(int animeId, AnimeField field, ref FieldCacheEntry<T> cacheEntry) where T : class
    {
        return cacheEntry.HasData ? cacheEntry.Value : null;
    }

    public FieldFetchState GetFieldState(int animeId, AnimeField field)
    {
        if (!_cache.TryGetValue(animeId, out var entry))
            return FieldFetchState.NotFetched;

        return field switch
        {
            AnimeField.TITLE => entry.Title.State,
            AnimeField.MAIN_PICTURE => entry.MainPicture.State,
            AnimeField.STATUS => entry.Status.State,
            AnimeField.SYNOPSIS => entry.Synopsis.State,
            AnimeField.ALTER_TITLES => entry.AlternativeTitles.State,
            AnimeField.MY_LIST_STATUS => entry.MyListStatus.State,
            AnimeField.EPISODES => entry.NumEpisodes.State,
            AnimeField.POPULARITY => entry.Popularity.State,
            AnimeField.PICTURE => entry.Picture.State,
            AnimeField.STUDIOS => entry.Studios.State,
            AnimeField.START_DATE => entry.StartDate.State,
            AnimeField.MEAN => entry.Mean.State,
            AnimeField.GENRES => entry.Genres.State,
            AnimeField.RELATED_ANIME => entry.RelatedAnime.State,
            AnimeField.VIDEOS => entry.Videos.State,
            AnimeField.NUM_FAV => entry.NumFavorites.State,
            AnimeField.STATS => entry.Statistics.State,
            AnimeField.TRAILER_URL => entry.TrailerURL.State,
            _ => FieldFetchState.NotFetched
        };
    }

    public string? GetTitle(int animeId)
    {
        var entry = GetOrCreateEntry(animeId);
        return GetOrFetchClass(animeId, AnimeField.TITLE, ref entry.Title);
    }

    public MAL_MainPicture? GetMainPicture(int animeId)
    {
        var entry = GetOrCreateEntry(animeId);
        return GetOrFetchClass(animeId, AnimeField.MAIN_PICTURE, ref entry.MainPicture);
    }

    public string? GetStatus(int animeId)
    {
        var entry = GetOrCreateEntry(animeId);
        return GetOrFetchClass(animeId, AnimeField.STATUS, ref entry.Status);
    }

    public string? GetSynopsis(int animeId)
    {
        var entry = GetOrCreateEntry(animeId);
        return GetOrFetchClass(animeId, AnimeField.SYNOPSIS, ref entry.Synopsis);
    }

    public MAL_AlternativeTitles? GetAlternativeTitles(int animeId)
    {
        var entry = GetOrCreateEntry(animeId);
        return GetOrFetchClass(animeId, AnimeField.ALTER_TITLES, ref entry.AlternativeTitles);
    }

    public MAL_MyListStatus? GetMyListStatus(int animeId)
    {
        var entry = GetOrCreateEntry(animeId);
        return GetOrFetchClass(animeId, AnimeField.MY_LIST_STATUS, ref entry.MyListStatus);
    }

    public int? GetNumEpisodes(int animeId)
    {
        var entry = GetOrCreateEntry(animeId);
        return GetOrFetchStruct(animeId, AnimeField.EPISODES, ref entry.NumEpisodes);
    }

    public int? GetPopularity(int animeId)
    {
        var entry = GetOrCreateEntry(animeId);
        return GetOrFetchStruct(animeId, AnimeField.POPULARITY, ref entry.Popularity);
    }

    public Bitmap? GetPicture(int animeId)
    {
        var entry = GetOrCreateEntry(animeId);
        return GetOrFetchClass(animeId, AnimeField.PICTURE, ref entry.Picture);
    }

    public MAL_Studio[]? GetStudios(int animeId)
    {
        var entry = GetOrCreateEntry(animeId);
        return GetOrFetchClass(animeId, AnimeField.STUDIOS, ref entry.Studios);
    }

    public string? GetStartDate(int animeId)
    {
        var entry = GetOrCreateEntry(animeId);
        return GetOrFetchClass(animeId, AnimeField.START_DATE, ref entry.StartDate);
    }

    public float? GetMean(int animeId)
    {
        var entry = GetOrCreateEntry(animeId);
        return GetOrFetchStruct(animeId, AnimeField.MEAN, ref entry.Mean);
    }

    public MAL_Genre[]? GetGenres(int animeId)
    {
        var entry = GetOrCreateEntry(animeId);
        return GetOrFetchClass(animeId, AnimeField.GENRES, ref entry.Genres);
    }

    public MAL_RelatedAnime[]? GetRelatedAnime(int animeId)
    {
        var entry = GetOrCreateEntry(animeId);
        return GetOrFetchClass(animeId, AnimeField.RELATED_ANIME, ref entry.RelatedAnime);
    }

    public MAL_Video[]? GetVideos(int animeId)
    {
        var entry = GetOrCreateEntry(animeId);
        return GetOrFetchClass(animeId, AnimeField.VIDEOS, ref entry.Videos);
    }

    public int? GetNumFavorites(int animeId)
    {
        var entry = GetOrCreateEntry(animeId);
        return GetOrFetchStruct(animeId, AnimeField.NUM_FAV, ref entry.NumFavorites);
    }

    public MAL_Statistics? GetStats(int animeId)
    {
        var entry = GetOrCreateEntry(animeId);
        return GetOrFetchClass(animeId, AnimeField.STATS, ref entry.Statistics);
    }

    public string? GetTrailerUrl(int animeId)
    {
        var entry = GetOrCreateEntry(animeId);
        return GetOrFetchClass(animeId, AnimeField.TRAILER_URL, ref entry.TrailerURL);
    }

    public void ClearCache() => _cache.Clear();

    public void RemoveAnime(int animeId) => _cache.TryRemove(animeId, out _);

    public IEnumerable<int> GetCachedAnimeIds() => _cache.Keys;

    public bool HasAnime(int animeId) => _cache.ContainsKey(animeId);
    
    public AnimeField[] GetMissingFields(int animeId, params AnimeField[] requestedFields)
    {
        var missing = new List<AnimeField>();
        
        foreach (var field in requestedFields)
        {
            if (GetFieldState(animeId, field) == FieldFetchState.NotFetched)
            {
                missing.Add(field);
            }
        }

        return missing.ToArray();
    }

    public async Task<AnimeFieldSet> GetFieldsAsync(int animeId, params AnimeField[] fields)
    {
        var missingFields = GetMissingFields(animeId, fields);

        if (missingFields.Length > 0 && _batchFetchFunc != null)
        {
            var fetchedAnime = await _batchFetchFunc(animeId, missingFields);
            if (fetchedAnime != null)
            {
                AddOrUpdate(fetchedAnime, missingFields);
            }
        }

        var result = new AnimeFieldSet(animeId, this, fields);
        return result;
    }
}