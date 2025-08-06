using Aniki.Misc;
using Avalonia.Media.Imaging;
using System.Text.Json.Serialization;

namespace Aniki.Models;

public class AnimeData
{
    public required AnimeNode Node { get; set; }
    [JsonPropertyName("list_status")] public ListStatus? ListStatus { get; set; }
    [JsonIgnore]
    public bool IsOnList => ListStatus != null;
}

public class AnimeNode
{
    public int Id { get; init; }
    public required string Title { get; init; }
    public Genre[]? Genres { get; set; }
}

public class AnimeListResponse
{
    public required AnimeData[] Data {get; set;}
    public Paging? Paging { get; set; }
}

public class AnimeSearchListResponse
{
    public required SearchEntry[] Data { get; set; }
    public Paging? Paging { get; set; }
}

public class SearchEntry
{
    [JsonPropertyName("node")]
    public required SearchAnimeNode Anime { get; set; }
}

public class SearchAnimeNode
{
    public int Id { get; set; }
    public required string Title { get; set; }
    [JsonPropertyName("main_picture")]
    public MainPicture? MainPicture { get; set; }
}

public class Paging
{
    public string? Next { get; set; }
}

public class AnimeDetails
{
    public int Id { get; set; }
    public required string Title { get; set; }
    [JsonPropertyName("main_picture")]
    public MainPicture? MainPicture { get; set; }
    public required string Status { get; set; }
    public required string Synopsis { get; set; }
    [JsonPropertyName("my_list_status")]
    public ListStatus? MyListStatus { get; set; }
    [JsonPropertyName("num_episodes")]
    public int NumEpisodes { get; set; }
    public Bitmap? Picture { get; set; }
    public Genre[]? Genres { get; set; }
}

public class MainPicture
{
    public required string Medium { get; set; }
    public required string Large { get; set; }
}

public class Genre
{
    public int Id { get; set; }
    public required string Name { get; set; }
}

public class ListStatus
{
    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AnimeStatusApi Status { get; set; }

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("num_episodes_watched")]
    public int NumEpisodesWatched { get; set; }
}

public class RelatedAnime
{
    [JsonPropertyName("node")]
    public AnimeNode? Node { get; set; }

    [JsonPropertyName("relation_type")]
    public required string RelationType { get; set; }

    [JsonPropertyName("relation_type_formatted")]
    public required string RelationTypeFormatted { get; set; }
}