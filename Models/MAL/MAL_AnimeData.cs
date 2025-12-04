using System.Text.Json.Serialization;
using Aniki.Converters;
using Aniki.Services.Cache;
using Avalonia.Media.Imaging;

namespace Aniki.Models.MAL;

public enum AnimeField 
{
    Id,
    Title, 
    MainPicture, 
    Status, 
    Synopsis, 
    AlterTitles, 
    MyListStatus, 
    Episodes,
    Popularity, 
    Picture, 
    Studios, 
    StartDate, 
    Mean, 
    Genres, 
    RelatedAnime, 
    Videos, 
    NumFav, 
    Stats, 
    TrailerUrl
}

public class MalUserAnimeListResponse
{
    public required MalAnimeNode[] Data { get; set; }
    public MalPaging? Paging { get; set; }
}

public class MalAnimeNode
{
    public required MalAnimeDetails Node { get; set; }
}


public class MalAnimeSearchListResponse
{
    public required MalSearchEntry[] Data { get; set; }
    public MalPaging? Paging { get; set; }
}

public class MalSearchEntry
{
    [JsonPropertyName("node")]
    public required MalAnimeDetails Node { get; set; }
}

public class MalPaging
{
    public string? Next { get; set; }
}

public class MalAnimeDetails : ObservableObject
{
    [CacheField(AnimeField.Id)] public int Id { get; set; }
    [CacheField(AnimeField.Title)] public string? Title { get; set; }
    [CacheField(AnimeField.MainPicture)][JsonPropertyName("main_picture")] public MalMainPicture? MainPicture { get; set; }
    [CacheField(AnimeField.Status)] public string? Status { get; set; }
    [CacheField(AnimeField.Synopsis)] public string? Synopsis { get; set; }
    [CacheField(AnimeField.AlterTitles)][JsonPropertyName("alternative_titles")] public MalAlternativeTitles? AlternativeTitles { get; set; }
    
    private MalMyListStatus? _myListStatus;
    [CacheField(AnimeField.MyListStatus, true)]
    [JsonPropertyName("my_list_status")]
    public MalMyListStatus? MyListStatus
    {
        get => _myListStatus;
        set
        {
            if (_myListStatus != value)
            {
                _myListStatus = value;
                OnPropertyChanged();
            }
        }
    }
    [CacheField(AnimeField.Episodes)][JsonPropertyName("num_episodes")] public int? NumEpisodes { get; set; }
    [CacheField(AnimeField.Popularity)] public int? Popularity { get; set; }
    [CacheField(AnimeField.Picture)] public Bitmap? Picture { get; set; }
    [CacheField(AnimeField.Studios)] public MalStudio[]? Studios { get; set; } 
    [CacheField(AnimeField.StartDate)][JsonPropertyName("start_date")] public string? StartDate { get; set; }
    private float _mean;
    [CacheField(AnimeField.Mean)] [JsonPropertyName("mean")]
    public float Mean 
    { 
        get => _mean;
        set => _mean = (float)Math.Round(value, 1);
    }
    [CacheField(AnimeField.Genres)] public MalGenre[]? Genres { get; set; }
    [CacheField(AnimeField.RelatedAnime)][JsonPropertyName("related_anime")] public MalRelatedAnime[]? RelatedAnime { get; set; }
    [CacheField(AnimeField.NumFav)][JsonPropertyName("num_favorites")] public int? NumFavorites { get; set; }
    [CacheField(AnimeField.Stats)] public MalStatistics? Statistics { get; set; }
    [CacheField(AnimeField.TrailerUrl)] public string? TrailerUrl { get; set; }
    [CacheField(AnimeField.Videos)] public AnimeVideo[]? Videos { get; set; }
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
    public MalAnimeDetails? Node { get; set; }

    [JsonPropertyName("relation_type")]
    public required string RelationType { get; set; }
}

public class MalStatistics
{
    [JsonPropertyName("num_list_users")]
    public int NumListUsers { get; set; }
    public MalStatusStatistics? Status { get; set; }

    public AnimeStatistics ToAnimeStatistics()
    {
        return new AnimeStatistics
        {
            NumListUsers = NumListUsers,
            StatusStats = new()
            {
                Watching = Status?.Watching ?? 0,
                Completed = Status?.Completed ?? 0,
                OnHold = Status?.OnHold ?? 0,
                Dropped = Status?.Dropped ?? 0,
                PlanToWatch = Status?.PlanToWatch ?? 0
            }
        };
    }
}

public class MalStatusStatistics
{
    [JsonConverter(typeof(MalIntStringConverter))]
    public int? Watching { get; set; }
    [JsonConverter(typeof(MalIntStringConverter))]
    public int? Completed { get; set; }
    [JsonPropertyName("on_hold")]
    [JsonConverter(typeof(MalIntStringConverter))]
    public int? OnHold { get; set; }
    [JsonConverter(typeof(MalIntStringConverter))]
    public int? Dropped { get; set; }
    [JsonPropertyName("plan_to_watch")]
    [JsonConverter(typeof(MalIntStringConverter))]
    public int? PlanToWatch { get; set; }
}