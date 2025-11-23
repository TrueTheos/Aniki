using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Aniki.Services.Interfaces;

public interface IVideoPlayerService
{
    public VideoPlayerOption? SelectedPlayer { get; set; }
    public ObservableCollection<VideoPlayerOption> AvailablePlayers { get; }
    public Process? OpenVideo(string url);
    public Task RefreshPlayersAsync();
}

public class VideoPlayerOption
{
    public string DisplayName { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    public bool IsSystemDefault { get; set; }

    public override string ToString() => DisplayName;
}