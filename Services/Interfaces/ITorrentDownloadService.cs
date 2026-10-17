using System.Collections.ObjectModel;

namespace Aniki.Services.Interfaces;

internal interface ITorrentDownloadService
{
    ObservableCollection<TorrentDownload> Downloads { get; }
    Task StartAsync(string torrentUrl, string saveFolder);
    Task CancelAsync(TorrentDownload download);
}
