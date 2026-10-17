using System.Collections.ObjectModel;
using System.Diagnostics;
using Aniki.Services.Interfaces;

namespace Aniki.ViewModels;

internal enum SortDirection
{
    Ascending,
    Descending
}

internal sealed partial class TorrentSearchViewModel : ViewModelBase
{
    [ObservableProperty] public partial bool IsTorrentsLoading { get; set; }
    [ObservableProperty] public partial string TorrentSearchTerms { get; set; } = string.Empty;
    [ObservableProperty] private ObservableCollection<NyaaTorrent> _torrentsList = [];
    [ObservableProperty] public partial KnownSubber SelectedSubber { get; set; } = KnownSubbers.All[0];
    [ObservableProperty] public partial SortDirection SeedersSortDirection { get; set; } = SortDirection.Descending;
    [ObservableProperty] public partial SortDirection DateSortDirection { get; set; } = SortDirection.Descending;

    public static IReadOnlyList<KnownSubber> AvailableSubbers => KnownSubbers.All;

    private enum TorrentSortField
    {
        Seeders,
        ReleaseDate
    }

    private AnimeDetails? _details;
    private List<NyaaTorrent> _allTorrents = [];
    private TorrentSortField _activeSortField = TorrentSortField.Seeders;
    
    private readonly INyaaService _nyaaService;
    private readonly ISaveService _saveService;
    private readonly ITorrentDownloadService _torrentDownloads;

    public TorrentSearchViewModel(INyaaService nyaaService, ISaveService saveService, ITorrentDownloadService torrentDownloads)
    {
        _nyaaService = nyaaService;
        _saveService = saveService;
        _torrentDownloads = torrentDownloads;
    }

    public void Update(AnimeDetails? details)
    {
        _details = details;
        TorrentSearchTerms = "";
        SelectedSubber = KnownSubbers.All[0];
        _allTorrents = [];
        TorrentsList.Clear();

        _ = SearchTorrents();
    }

    partial void OnSelectedSubberChanged(KnownSubber value) => ApplyFilterAndSort();

    [RelayCommand]
    public async Task SearchTorrents()
    {
        if (_details == null) return;
        
        IsTorrentsLoading = true;

        TorrentsList.Clear();

        try
        {
            if (_details.Title != null)
            {
                var foundTorrents = await _nyaaService.SearchAsync(_details.Title, TorrentSearchTerms).ConfigureAwait(true);
                foreach (NyaaTorrent torrent in foundTorrents)
                    TorrentFileNameFormatter.ApplyDisplayMetadata(torrent);

                _allTorrents = foundTorrents;
                ApplyFilterAndSort();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Torrent search failed: {ex.Message}");
        }
        finally
        {
            IsTorrentsLoading = false;
        }
    }

    [RelayCommand]
    public void SortBySeeders()
    {
        _activeSortField = TorrentSortField.Seeders;

        SeedersSortDirection = SeedersSortDirection == SortDirection.Descending ? SortDirection.Ascending : SortDirection.Descending;

        ApplyFilterAndSort();
    }

    [RelayCommand]
    public void SortByReleaseDate()
    {
        _activeSortField = TorrentSortField.ReleaseDate;

        DateSortDirection = DateSortDirection == SortDirection.Descending ? SortDirection.Ascending : SortDirection.Descending;

        ApplyFilterAndSort();
    }

    private void ApplyFilterAndSort()
    {
        if (_allTorrents.Count == 0)
        {
            TorrentsList = [];
            return;
        }

        IEnumerable<NyaaTorrent> sortedTorrents = _allTorrents;

        if (!SelectedSubber.IsAll)
        {
            sortedTorrents = sortedTorrents.Where(t =>
                !string.IsNullOrEmpty(t.ReleaseGroup) &&
                string.Equals(t.ReleaseGroup, SelectedSubber.Name, StringComparison.OrdinalIgnoreCase));
        }

        sortedTorrents = _activeSortField switch
        {
            TorrentSortField.Seeders when SeedersSortDirection == SortDirection.Descending =>
                sortedTorrents.OrderByDescending(x => x.Seeders),
            TorrentSortField.Seeders => sortedTorrents.OrderBy(x => x.Seeders),
            TorrentSortField.ReleaseDate when DateSortDirection == SortDirection.Descending =>
                sortedTorrents.OrderByDescending(x => x.PublishDate),
            _ => sortedTorrents.OrderBy(x => x.PublishDate)
        };

        TorrentsList = new ObservableCollection<NyaaTorrent>(sortedTorrents);
    }

    [RelayCommand]
    private async Task DownloadTorrent(string torrentLink)
    {
        if (!Uri.TryCreate(torrentLink, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != "magnet"))
            return;

        bool builtIn = uri.Scheme == Uri.UriSchemeHttps && _saveService.GetSettingsConfig()?.UseBuiltInDownloader != false;
        if (!builtIn)
        {
            Process.Start(new ProcessStartInfo(torrentLink) { UseShellExecute = true });
            return;
        }

        try
        {
            await _torrentDownloads.StartAsync(torrentLink, _saveService.EpisodesFolder).ConfigureAwait(true);
            _ = ToastService.Show("Downloading - progress is in the Downloaded tab");
        }
        catch (Exception ex)
        {
            _ = ToastService.Show($"Download failed: {ex.Message}");
        }
    }

    public override Task Enter()
    {
        return Task.CompletedTask;
    }
}