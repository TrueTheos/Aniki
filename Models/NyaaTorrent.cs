namespace Aniki.Models;

public class NyaaTorrent
{
    public required string FileName { get; set; }
    public required string TorrentLink { get; set; }
    public required string Size { get; set; }
    public int Seeders { get; set; }
    public DateTime PublishDate { get; set; }
}