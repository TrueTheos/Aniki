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

    public TorrentSearchViewModel(INyaaService nyaaService)
    {
        _nyaaService = nyaaService;
    }

    public void Update(AnimeDetails? details, int episodesWatched)
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

        if (_details.Title != null)
        {
            var foundTorrents = await _nyaaService.SearchAsync(_details.Title, TorrentSearchTerms).ConfigureAwait(true);
            foreach (NyaaTorrent torrent in foundTorrents)
                TorrentFileNameFormatter.ApplyDisplayMetadata(torrent);

            _allTorrents = foundTorrents;
            ApplyFilterAndSort();
        }

        IsTorrentsLoading = false;
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
    public static void DownloadTorrent(string magnet)
    {
        Process.Start(new ProcessStartInfo(magnet) { UseShellExecute = true });
    }

    public override Task Enter()
    {
        return Task.CompletedTask;
    }
}