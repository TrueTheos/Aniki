using System.Text.Json.Serialization;

namespace Aniki.Models.MAL;

public class MAL_Statistics
{
    [JsonPropertyName("num_list_users")]
    public int NumListUsers { get; set; }
    public MAL_StatusStatistics? Status { get; set; }

    public AnimeStatistics ToAnimeStatistics()
    {
        return new AnimeStatistics
        {
            NumListUsers = NumListUsers,
            StatusStats = new()
            {
                Watching    = Status?.Watching ?? 0,
                Completed   = Status?.Completed ?? 0,
                OnHold      = Status?.OnHold ?? 0,
                Dropped     = Status?.Dropped ?? 0,
                PlanToWatch = Status?.PlanToWatch ?? 0
            }
        };
    }
}