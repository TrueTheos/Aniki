using System.Text.Json.Serialization;

namespace Aniki.Models.MAL.Components;

public class MAL_SearchEntry
{
    [JsonPropertyName("node")]
    public required MAL_Anime Node { get; set; }
}