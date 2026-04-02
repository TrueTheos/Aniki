using System.Text.Json.Serialization;
using Aniki.Converters;

namespace Aniki.Models.MAL;

public class MAL_StatusStatistics
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