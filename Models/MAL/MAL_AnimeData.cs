using System.Text.Json.Serialization;
using Aniki.Misc;
using Avalonia.Media.Imaging;

namespace Aniki.Models.MAL;

public class MAL_AnimeData
{
    public required MAL_AnimeNode Node { get; set; }
    [JsonPropertyName("list_status")] public MAL_MyListStatus? ListStatus { get; set; }
    [JsonIgnore]
    public bool IsOnList => ListStatus != null;
}

public class MAL_AnimeNode
{
    public int Id { get; init; }
    public required string Title { get; init; }
    [JsonPropertyName("alternative_titles")]
    public MAL_AlternativeTitles? AlternativeTitles { get; set; }
    public MAL_Genre[]? Genres { get; set; }
    public required string Synopsis { get; set; }
    public required string Status { get; set; }
    [JsonPropertyName("main_picture")]
    public MAL_MainPicture? MainPicture { get; set; }
    [JsonPropertyName("num_episodes")]
    public int NumEpisodes { get; set; }
    public int Popularity { get; set; }
    public MAL_Video[]? Videos { get; set; }
    private float _mean;
    [JsonPropertyName("mean")]
    public float Mean 
    { 
        get => _mean;
        set => _mean = (float)Math.Round(value, 1);
    }
}

public class MAL_UserAnimeListResponse
{
    public required MAL_AnimeData[] Data {get; set;}
    public MAL_Paging? Paging { get; set; }
}

public class MAL_AnimeSearchListResponse
{
    public required MAL_SearchEntry[] Data { get; set; }
    public MAL_Paging? Paging { get; set; }
}

public class MAL_SearchEntry
{
    [JsonPropertyName("node")]
    public required MAL_AnimeNode MalAnime { get; set; }
}

public class MAL_Paging
{
    public string? Next { get; set; }
}

public class MAL_AnimeDetails
{
    public required int Id { get; init; }
    public required string Title { get; set; }
    [JsonPropertyName("main_picture")]
    public MAL_MainPicture? MainPicture { get; init; }
    public required string Status { get; init; }
    public required string Synopsis { get; init; }
    [JsonPropertyName("alternative_titles")]
    public MAL_AlternativeTitles? AlternativeTitles { get; init; }
    
    [JsonPropertyName("my_list_status")]
    public MAL_MyListStatus? MyListStatus { get; set; }
    [JsonPropertyName("num_episodes")]
    public required int NumEpisodes { get; init; }
    public required int Popularity { get; init; }
    public Bitmap? Picture { get; set; }
    public MAL_Studio[]? Studios { get; set; } 
    [JsonPropertyName("start_date")]
    public string? StartDate { get; set; }
    private float _mean;
    [JsonPropertyName("mean")]
    public float Mean
    { 
        get => _mean;
        set => _mean = (float)Math.Round(value, 1);
    }
    public MAL_Genre[]? Genres { get; set; }
    [JsonPropertyName("related_anime")]
    public MAL_RelatedAnime[]? RelatedAnime { get; set; }
    public MAL_Video[]? Videos { get; set; }

    public AnimeCardData ToCardData()
    {
        return new AnimeCardData()
        {
            AnimeId = Id,
            Title = Title,
            ImageUrl = MainPicture!.Large != null ? MainPicture.Large : MainPicture.Medium,
            Score = Mean,
            Status = MyListStatus != null ? MyListStatus.Status : AnimeStatusApi.none
        };
    }
}

public class MAL_Studio
{
    public int Id { get; set; }
    public required string Name { get; set; }
}

public class MAL_MainPicture
{
    public required string Medium { get; set; }
    public required string Large { get; set; }
}

public class MAL_Genre
{
    public int Id { get; set; }
    public required string Name { get; set; }
}

public class MAL_AlternativeTitles
{
    [JsonPropertyName("synonyms")]
    public string[]? Synonyms { get; set; }
    [JsonPropertyName("en")]
    public string? En { get; set; }
    [JsonPropertyName("ja")]
    public string? Ja { get; set; }
}

public class MAL_MyListStatus
{
    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AnimeStatusApi Status { get; set; }

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("num_episodes_watched")]
    public int NumEpisodesWatched { get; set; }
}

public class MAL_RelatedAnime
{
    [JsonPropertyName("node")]
    public MAL_AnimeNode? Node { get; set; }

    [JsonPropertyName("relation_type")]
    public required string RelationType { get; set; }
}

public class MAL_Video
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