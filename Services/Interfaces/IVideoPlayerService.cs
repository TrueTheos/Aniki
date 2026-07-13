using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Aniki.Services.Interfaces;

internal interface IVideoPlayerService
{
    public VideoPlayerOption? SelectedPlayer { get; set; }
    public ObservableCollection<VideoPlayerOption> AvailablePlayers { get; }
    public Process? OpenVideo(string url);
    public Task ScanPlayersAsync();
}

internal sealed class VideoPlayerOption
{
    public string DisplayName { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    public bool IsSystemDefault { get; set; }

    public override string ToString() => DisplayName;
}