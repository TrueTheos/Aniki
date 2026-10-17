using MonoTorrent.Client;

namespace Aniki.Models;

internal sealed partial class TorrentDownload(TorrentManager manager) : ObservableObject
{
    public TorrentManager Manager { get; } = manager;
    public string Name => Manager.Torrent?.Name ?? "";

    [ObservableProperty] public partial double Progress { get; set; }
    [ObservableProperty] public partial string Status { get; set; } = "Starting";
}
