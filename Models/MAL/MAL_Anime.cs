using System.Text.Json.Serialization;
using Aniki.Models.MAL.Components;

namespace Aniki.Models.MAL;

internal sealed class MAL_Anime
{
    public int Id { get; set; }
    public string? Title { get; set; }
    [JsonPropertyName("main_picture")] public MAL_MainPicture? MainPicture { get; set; }
    public string? Status { get; set; }
    public string? Synopsis { get; set; }
    [JsonPropertyName("alternative_titles")] public MAL_AlternativeTitles? AlternativeTitles { get; set; }

    [JsonPropertyName("my_list_status")]
    public MAL_MyListStatus? MyListStatus { get; set; }

    [JsonPropertyName("num_episodes")] public int? NumEpisodes { get; set; }
    public int? Popularity { get; set; }
    public MAL_Studio[]? Studios { get; set; } 
    [JsonPropertyName("start_date")] public string? StartDate { get; set; }

    [JsonPropertyName("mean")]
    public float Mean
    {
        get;
        set => field = (float)Math.Round(value, 1);
    }

    public MAL_Genre[]? Genres { get; set; }
    [JsonPropertyName("related_anime")] public MAL_RelatedAnime[]? RelatedAnime { get; set; }
    [JsonPropertyName("num_favorites")] public int? NumFavorites { get; set; }
    public MAL_Statistics? Statistics { get; set; }
    public string? TrailerUrl { get; set; }
    public AnimeVideo[]? Videos { get; set; }

    [JsonPropertyName("media_type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MediaType? MediaType { get; set; }
}