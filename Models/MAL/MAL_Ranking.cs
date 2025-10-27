using System.Text.Json.Serialization;

namespace Aniki.Models.MAL;

public class MAL_AnimeRankingResponse
{
    public required MAL_RankingEntry[] Data { get; set; }
    public MAL_Paging? Paging { get; set; }
}

public class MAL_RankingEntry
{
    [JsonPropertyName("node")]
    public required MAL_AnimeNode Node { get; set; }
    
    [JsonPropertyName("ranking")]
    public MAL_RankingInfo? Ranking { get; set; }
}

public class MAL_RankingInfo
{
    [JsonPropertyName("rank")]
    public int Rank { get; set; }
}