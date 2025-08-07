using System.Text.Json.Serialization;

namespace Aniki.Models;

public class MALUserData
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Picture { get; set; }
    [JsonPropertyName("anime_statistics")]
    public AnimeStatistics? AnimeStatistics { get; set; }
}

public class AnimeStatistics
{
    [JsonPropertyName("num_items_watching")]
    public int NumItemsWatching { get; set; }

    [JsonPropertyName("num_items_completed")]
    public int NumItemsCompleted { get; set; }

    [JsonPropertyName("num_items_on_hold")]
    public int NumItemsOnHold { get; set; }

    [JsonPropertyName("num_items_dropped")]
    public int NumItemsDropped { get; set; }

    [JsonPropertyName("num_items_plan_to_watch")]
    public int NumItemsPlanToWatch { get; set; }

    [JsonPropertyName("num_days_watched")]
    public double NumDaysWatched { get; set; }

    [JsonPropertyName("num_days_watching")]
    public double NumDaysWatching { get; set; }

    [JsonPropertyName("num_days_completed")]
    public double NumDaysCompleted { get; set; }

    [JsonPropertyName("num_days_on_hold")]
    public double NumDaysOnHold { get; set; }

    [JsonPropertyName("num_days_dropped")]
    public double NumDaysDropped { get; set; }

    [JsonPropertyName("num_days_plan_to_watch")]
    public double NumDaysPlanToWatch { get; set; }

    [JsonPropertyName("num_episodes")]
    public int NumEpisodes { get; set; }

    [JsonPropertyName("num_times_rewatched")]
    public int NumTimesRewatched { get; set; }

    [JsonPropertyName("mean_score")]
    public double MeanScore { get; set; }
}