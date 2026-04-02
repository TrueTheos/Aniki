using System.Text.Json.Serialization;

namespace Aniki.Models.MAL;

public class MalAnimeRankingResponse
{
    public required MalRankingEntry[] Data { get; set; }
    public MAL_Paging? Paging { get; set; }
}

public class MalRankingEntry
{
    [JsonPropertyName("node")]
    public required MAL_Anime Node { get; set; }
    
    [JsonPropertyName("ranking")]
    public MalRankingInfo? Ranking { get; set; }
}

public class MalRankingInfo
{
    [JsonPropertyName("rank")]
    public int Rank { get; set; }
}