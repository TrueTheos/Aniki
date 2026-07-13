using System.Text.Json.Serialization;

namespace Aniki.Models.MAL.Components;

internal sealed class MAL_AlternativeTitles
{
    [JsonPropertyName("synonyms")]
    public string[]? Synonyms { get; set; }
    [JsonPropertyName("en")]
    public string? En { get; set; }
    [JsonPropertyName("ja")]
    public string? Ja { get; set; }
}