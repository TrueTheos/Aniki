using System.Text.Json.Serialization;
using Aniki.Services.Cache;
using Avalonia.Media.Imaging;

namespace Aniki.Models.MAL;

public class MAL_Anime : ObservableObject
{
    [CacheField(AnimeField.Id)] public int Id { get; set; }
    [CacheField(AnimeField.Title)] public string? Title { get; set; }
    [CacheField(AnimeField.MainPicture)][JsonPropertyName("main_picture")] public MAL_MainPicture? MainPicture { get; set; }
    [CacheField(AnimeField.Status)] public string? Status { get; set; }
    [CacheField(AnimeField.Synopsis)] public string? Synopsis { get; set; }
    [CacheField(AnimeField.AlterTitles)][JsonPropertyName("alternative_titles")] public MAL_AlternativeTitles? AlternativeTitles { get; set; }

    [CacheField(AnimeField.MyListStatus, true)]
    [JsonPropertyName("my_list_status")]
    public MAL_MyListStatus? MyListStatus
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    }

    [CacheField(AnimeField.Episodes)][JsonPropertyName("num_episodes")] public int? NumEpisodes { get; set; }
    [CacheField(AnimeField.Popularity)] public int? Popularity { get; set; }
    [CacheField(AnimeField.Picture)] public Bitmap? Picture { get; set; }
    [CacheField(AnimeField.Studios)] public MAL_Studio[]? Studios { get; set; } 
    [CacheField(AnimeField.StartDate)][JsonPropertyName("start_date")] public string? StartDate { get; set; }

    [CacheField(AnimeField.Mean)]
    [JsonPropertyName("mean")]
    public float Mean
    {
        get;
        set => field = (float)Math.Round(value, 1);
    }

    [CacheField(AnimeField.Genres)] public MAL_Genre[]? Genres { get; set; }
    [CacheField(AnimeField.RelatedAnime)][JsonPropertyName("related_anime")] public MAL_RelatedAnime[]? RelatedAnime { get; set; }
    [CacheField(AnimeField.NumFav)][JsonPropertyName("num_favorites")] public int? NumFavorites { get; set; }
    [CacheField(AnimeField.Stats)] public MAL_Statistics? Statistics { get; set; }
    [CacheField(AnimeField.TrailerUrl)] public string? TrailerUrl { get; set; }
    [CacheField(AnimeField.Videos)] public AnimeVideo[]? Videos { get; set; }

    [CacheField(AnimeField.MediaType)]
    [JsonPropertyName("media_type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MediaType? MediaType { get; set; }
}