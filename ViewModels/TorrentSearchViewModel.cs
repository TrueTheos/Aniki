using System.Collections.ObjectModel;
using System.Diagnostics;
using Aniki.Services.Interfaces;

namespace Aniki.ViewModels;

public enum SortDirection
{
    Ascending,
    Descending
}

public partial class TorrentSearchViewModel : ViewModelBase
{
    [ObservableProperty] private bool _isTorrentsLoading;
    [ObservableProperty] private string _torrentSearchTerms = string.Empty;
    [ObservableProperty] private ObservableCollection<NyaaTorrent> _torrentsList = new();
    [ObservableProperty] private KnownSubber _selectedSubber = KnownSubbers.All[0];

    public IReadOnlyList<KnownSubber> AvailableSubbers { get; } = KnownSubbers.All;

    private enum TorrentSortField
    {
        Seeders,
        ReleaseDate
    }

    private AnimeDetails? _details;
    private List<NyaaTorrent> _allTorrents = [];
    private TorrentSortField _activeSortField = TorrentSortField.Seeders;

    [ObservableProperty]
    private SortDirection _seedersSortDirection = SortDirection.Descending;

    [ObservableProperty]
    private SortDirection _dateSortDirection = SortDirection.Descending;
    
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
            List<NyaaTorrent> list = await _nyaaService.SearchAsync(_details.Title, TorrentSearchTerms);
            foreach (NyaaTorrent torrent in list)
                TorrentFileNameFormatter.ApplyDisplayMetadata(torrent);

            _allTorrents = list;
            ApplyFilterAndSort();
        }

        IsTorrentsLoading = false;
    }

    [RelayCommand]
    public void SortBySeeders()
    {
        _activeSortField = TorrentSortField.Seeders;

        if (SeedersSortDirection == SortDirection.Descending)
            SeedersSortDirection = SortDirection.Ascending;
        else
            SeedersSortDirection = SortDirection.Descending;

        ApplyFilterAndSort();
    }

    [RelayCommand]
    public void SortByReleaseDate()
    {
        _activeSortField = TorrentSortField.ReleaseDate;

        if (DateSortDirection == SortDirection.Descending)
            DateSortDirection = SortDirection.Ascending;
        else
            DateSortDirection = SortDirection.Descending;

        ApplyFilterAndSort();
    }

    private void ApplyFilterAndSort()
    {
        if (_allTorrents.Count == 0)
        {
            TorrentsList = [];
            return;
        }

        IEnumerable<NyaaTorrent> query = _allTorrents;

        if (!SelectedSubber.IsAll)
        {
            query = query.Where(t =>
                !string.IsNullOrEmpty(t.ReleaseGroup) &&
                string.Equals(t.ReleaseGroup, SelectedSubber.Name, StringComparison.OrdinalIgnoreCase));
        }

        query = _activeSortField switch
        {
            TorrentSortField.Seeders when SeedersSortDirection == SortDirection.Descending =>
                query.OrderByDescending(x => x.Seeders),
            TorrentSortField.Seeders => query.OrderBy(x => x.Seeders),
            TorrentSortField.ReleaseDate when DateSortDirection == SortDirection.Descending =>
                query.OrderByDescending(x => x.PublishDate),
            _ => query.OrderBy(x => x.PublishDate)
        };

        TorrentsList = new ObservableCollection<NyaaTorrent>(query);
    }

    [RelayCommand]
    public void DownloadTorrent(string magnet)
    {
        Process.Start(new ProcessStartInfo(magnet) { UseShellExecute = true });
    }

    public override Task Enter()
    {
        return Task.CompletedTask;
    }
}