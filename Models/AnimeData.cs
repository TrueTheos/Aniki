using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Aniki.Models
{
    public class AnimeData
    {
        public AnimeNode Node { get; set; }
        [JsonPropertyName("list_status")]
        public ListStatus ListStatus { get; set; }
    }

    public class AnimeNode
    {
        public int Id { get; set; }
        public string Title { get; set; }

        public string MainPictureUrl { get; set; }
        public int TotalEpisodes { get; set; }
        public string Description { get; set; }
    }

    public class ListStatus
    {
        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("score")]
        public int Score { get; set; }

        [JsonPropertyName("num_episodes_watched")]
        public int Num_Episodes_Watched { get; set; }
    }
}
