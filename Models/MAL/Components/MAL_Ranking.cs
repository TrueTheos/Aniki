using System.Text.Json.Serialization;

namespace Aniki.Models.MAL.Components;

internal sealed class MalAnimeRankingResponse
{
    public required MalRankingEntry[] Data { get; set; }
    public MAL_Paging? Paging { get; set; }
}

internal sealed class MalRankingEntry
{
    [JsonPropertyName("node")]
    public required MAL_Anime Node { get; set; }
}