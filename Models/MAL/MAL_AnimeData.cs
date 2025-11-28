using System.Text.Json.Serialization;
using Aniki.Converters;
using Avalonia.Media.Imaging;

namespace Aniki.Models.MAL;

public enum AnimeField 
{
    ID,
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

public class MalAnimeData
{
    public required MalAnimeNode Node { get; set; }
    [JsonPropertyName("list_status")] public MalMyListStatus? ListStatus { get; set; }
    [JsonIgnore]
    public bool IsOnList => ListStatus != null;
}

public class MalAnimeNode
{
    [CacheField(AnimeField.ID)] public int Id { get; init; }
    [CacheField(AnimeField.TITLE)] public string? Title { get; init; }
    [CacheField(AnimeField.ALTER_TITLES)][JsonPropertyName("alternative_titles")] public MalAlternativeTitles? AlternativeTitles { get; set; }
    [CacheField(AnimeField.GENRES)] public MalGenre[]? Genres { get; set; }
    [CacheField(AnimeField.MY_LIST_STATUS)] [JsonPropertyName("my_list_status")] public MalMyListStatus? MyListStatus { get; set; }
    [CacheField(AnimeField.SYNOPSIS)] public string? Synopsis { get; set; }
    [CacheField(AnimeField.STATUS)] public string? Status { get; set; }
    [CacheField(AnimeField.MAIN_PICTURE)] [JsonPropertyName("main_picture")] public MalMainPicture? MainPicture { get; set; }
    [CacheField(AnimeField.EPISODES)] [JsonPropertyName("num_episodes")] public int NumEpisodes { get; set; }
    [CacheField(AnimeField.POPULARITY)] int Popularity { get; set; }
    [CacheField(AnimeField.VIDEOS)] public MalVideo[]? Videos { get; set; }
    [CacheField(AnimeField.START_DATE)] [JsonPropertyName("start_date")] public string? StartDate { get; set; }
    [CacheField(AnimeField.STUDIOS)] public MalStudio[]? Studios { get; set; } 
    private float _mean;
    [CacheField(AnimeField.MEAN)] [JsonPropertyName("mean")]
    public float Mean 
    { 
        get => _mean;
        set => _mean = (float)Math.Round(value, 1);
    }

    public AnimeCardData ToCardData()
    {
        return new AnimeCardData()
        {
            AnimeId = Id,
            Title = Title,
            ImageUrl = MainPicture == null ? null : string.IsNullOrEmpty(MainPicture.Large) ? MainPicture.Medium : MainPicture.Large,
            Score = Mean,
            MyListStatus = MyListStatus?.Status ?? AnimeStatusApi.none
        };
    }
}

public class MalUserAnimeListResponse
{
    public required MalAnimeData[] Data {get; set;}
    public MalPaging? Paging { get; set; }
}

public class MalAnimeSearchListResponse
{
    public required MalSearchEntry[] Data { get; set; }
    public MalPaging? Paging { get; set; }
}

public class MalSearchEntry
{
    [JsonPropertyName("node")]
    public required MalAnimeNode Node { get; set; }
}

public class MalPaging
{
    public string? Next { get; set; }
}

public class MalAnimeDetails
{
    [CacheField(AnimeField.ID)] public int Id { get; set; }
    [CacheField(AnimeField.TITLE)] public string? Title { get; set; }
    [CacheField(AnimeField.MAIN_PICTURE)][JsonPropertyName("main_picture")] public MalMainPicture? MainPicture { get; set; }
    [CacheField(AnimeField.STATUS)]public string? Status { get; set; }
    [CacheField(AnimeField.SYNOPSIS)]public string? Synopsis { get; set; }
    [CacheField(AnimeField.ALTER_TITLES)][JsonPropertyName("alternative_titles")] public MalAlternativeTitles? AlternativeTitles { get; set; }
    
    [CacheField(AnimeField.MY_LIST_STATUS)][JsonPropertyName("my_list_status")] public MalMyListStatus? MyListStatus { get; set; }
    [CacheField(AnimeField.EPISODES)][JsonPropertyName("num_episodes")] public int? NumEpisodes { get; set; }
    [CacheField(AnimeField.POPULARITY)] public int? Popularity { get; set; }
    [CacheField(AnimeField.PICTURE)] public Bitmap? Picture { get; set; }
    [CacheField(AnimeField.STUDIOS)] public MalStudio[]? Studios { get; set; } 
    [CacheField(AnimeField.START_DATE)][JsonPropertyName("start_date")] public string? StartDate { get; set; }
    [CacheField(AnimeField.MEAN)] public float? Mean { get; set; }
    [CacheField(AnimeField.GENRES)] public MalGenre[]? Genres { get; set; }
    [CacheField(AnimeField.RELATED_ANIME)][JsonPropertyName("related_anime")] public MalRelatedAnime[]? RelatedAnime { get; set; }
    [CacheField(AnimeField.VIDEOS)] public MalVideo[]? Videos { get; set; }
    [CacheField(AnimeField.NUM_FAV)][JsonPropertyName("num_favorites")] public int? NumFavorites { get; set; }
    [CacheField(AnimeField.STATS)] public MalStatistics? Statistics { get; set; }
    [CacheField(AnimeField.TRAILER_URL)] public string? TrailerUrl { get; set; }
}

public class MalStudio
{
    public int Id { get; set; }
    public required string Name { get; set; }
}

public class MalMainPicture
{
    public required string Medium { get; set; }
    public required string Large { get; set; }
}

public class MalGenre
{
    public int Id { get; set; }
    public required string Name { get; set; }
}

public class MalAlternativeTitles
{
    [JsonPropertyName("synonyms")]
    public string[]? Synonyms { get; set; }
    [JsonPropertyName("en")]
    public string? En { get; set; }
    [JsonPropertyName("ja")]
    public string? Ja { get; set; }
}

public class MalMyListStatus
{
    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AnimeStatusApi Status { get; set; }

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("num_episodes_watched")]
    public int NumEpisodesWatched { get; set; }
}

public class MalRelatedAnime
{
    [JsonPropertyName("node")]
    public MalAnimeNode? Node { get; set; }

    [JsonPropertyName("relation_type")]
    public required string RelationType { get; set; }
}

public class MalVideo
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Url { get; set; }
    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }
    [JsonPropertyName("updated_at")]
    public long UpdatedAt { get; set; }
    public required string Thumbnail { get; set; }
}

public class MalStatistics
{
    [JsonPropertyName("num_list_users")]
    public int NumListUsers { get; set; }
    public MalStatusStatistics? Status { get; set; }
}

public class MalStatusStatistics
{
    [JsonConverter(typeof(IntToStringConverter))]
    public string? Watching { get; set; }
    [JsonConverter(typeof(IntToStringConverter))]
    public string? Completed { get; set; }
    [JsonPropertyName("on_hold")]
    [JsonConverter(typeof(IntToStringConverter))]
    public string? OnHold { get; set; }
    [JsonConverter(typeof(IntToStringConverter))]
    public string? Dropped { get; set; }
    [JsonPropertyName("plan_to_watch")]
    [JsonConverter(typeof(IntToStringConverter))]
    public string? PlanToWatch { get; set; }
}