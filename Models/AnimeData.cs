using Avalonia.Media.Imaging;
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

        public int TotalEpisodes { get; set; }
    }


    public class AnimeListResponse
    {
        public AnimeData[] Data { get; set; }
        public Paging Paging { get; set; }
    }

    public class AnimeSearchListResponse
    {
        public SearchEntry[] Data { get; set; }
        public Paging Paging { get; set; }
    }

    public class SearchEntry
    {
        [JsonPropertyName("node")]
        public SearchAnimeNode Anime { get; set; }
    }

    public class SearchAnimeNode
    {
        public int Id { get; set; }
        public string Title { get; set; }
        [JsonPropertyName("main_picture")]
        public MainPicture MainPicture { get; set; }
    }

    public class Paging
    {
        public string Next { get; set; }
    }

    public class AnimeDetails
    {
        public int Id { get; set; }
        public string Title { get; set; }
        [JsonPropertyName("main_picture")]
        public MainPicture MainPicture { get; set; }
        public string Status { get; set; }
        public string Synopsis { get; set; }
        [JsonPropertyName("my_list_status")]
        public ListStatus MyListStatus { get; set; }
        [JsonPropertyName("num_episodes")]
        public int NumEpisodes { get; set; }
        public Bitmap Picture { get; set; }
        public Genre[] Genres { get; set; }
    }

    public class MainPicture
    {
        public string Medium { get; set; }
        public string Large { get; set; }
    }

    public class Genre
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class ListStatus
    {
        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("score")]
        public int Score { get; set; }

        [JsonPropertyName("num_episodes_watched")]
        public int NumEpisodesWatched { get; set; }
    }
}
