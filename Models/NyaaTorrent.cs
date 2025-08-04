namespace Aniki.Models
{
    public class NyaaTorrent
    {
        public string FileName { get; set; }
        public string AnimeTitle { get; set; }
        public string? EpisodeNumber { get; set; }
        public string TorrentLink { get; set; }
        public string Size { get; set; }
        public int Seeders { get; set; }
    }
}
