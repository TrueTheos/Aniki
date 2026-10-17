using System.Collections.ObjectModel;
using System.Globalization;
using Aniki.Services.Interfaces;
using Aniki.Services.Save;
using Avalonia.Threading;
using MonoTorrent;
using MonoTorrent.Client;

namespace Aniki.Services;

internal sealed class TorrentDownloadService : ITorrentDownloadService, IDisposable
{
    private readonly ClientEngine _engine = new(new EngineSettingsBuilder
    {
        CacheDirectory = Path.Combine(SaveService.CachePath, "torrents"),
        UsePartialFiles = true
    }.ToSettings());

    private readonly HttpClient _http = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    public ObservableCollection<TorrentDownload> Downloads { get; } = [];

    public TorrentDownloadService()
    {
        _timer.Tick += (_, _) => _ = RefreshAsync();
    }

    public async Task StartAsync(string torrentUrl, string saveFolder)
    {
        byte[] data = await _http.GetByteArrayAsync(torrentUrl).ConfigureAwait(true);
        Torrent torrent = await Torrent.LoadAsync(data).ConfigureAwait(true);
        TorrentManager manager = await _engine.AddAsync(torrent, saveFolder).ConfigureAwait(true);
        await manager.StartAsync().ConfigureAwait(true);

        Downloads.Add(new TorrentDownload(manager));
        _timer.Start();
    }

    public async Task CancelAsync(TorrentDownload download)
    {
        if (!Downloads.Remove(download)) return;
        if (Downloads.Count == 0) _timer.Stop();

        TorrentManager manager = download.Manager;
        await manager.StopAsync().ConfigureAwait(true);
        await _engine.RemoveAsync(manager,
            manager.Complete ? RemoveMode.CacheDataOnly : RemoveMode.CacheDataAndDownloadedData).ConfigureAwait(true);
    }

    private async Task RefreshAsync()
    {
        try
        {
            foreach (TorrentDownload download in Downloads.ToList())
            {
                TorrentManager manager = download.Manager;
                download.Progress = manager.Progress;
                download.Status = manager.State switch
                {
                    TorrentState.Downloading => string.Create(CultureInfo.InvariantCulture,
                        $"{manager.Progress:F0}% · {manager.Monitor.DownloadRate / 1048576.0:F1} MB/s"),
                    TorrentState.Seeding => "Done · sharing back to other users",
                    _ => manager.State.ToString()
                };

                if (manager.Complete && manager.Monitor.DataBytesSent >= manager.Torrent!.Size)
                    await CancelAsync(download).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Torrent refresh failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _engine.Dispose();
        _http.Dispose();
    }
}
