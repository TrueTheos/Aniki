using System.Text.Json.Serialization;

namespace Aniki.Models.MAL;

public class MAL_RelatedAnime
{
    [JsonPropertyName("node")]
    public MAL_Anime? Node { get; set; }

    [JsonPropertyName("relation_type")]
    public required string RelationType { get; set; }
}