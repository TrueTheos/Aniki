using Aniki.Services.Interfaces;
using System.Collections.ObjectModel;
using System.Diagnostics;

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
    
    private MalAnimeDetails? _details;

    [ObservableProperty]
    private SortDirection _seedersSortDirection = SortDirection.Descending;

    [ObservableProperty]
    private SortDirection _dateSortDirection = SortDirection.Descending;
    
    private readonly INyaaService _nyaaService;

    public TorrentSearchViewModel(INyaaService nyaaService)
    {
        _nyaaService = nyaaService;
    }

    public void Update(MalAnimeDetails? details, int episodesWatched)
    {
        _details = details;
        
        TorrentsList.Clear();

        _ = SearchTorrents();
    }

    [RelayCommand]
    public async Task SearchTorrents()
    {
        if (_details == null) return;
        
        IsTorrentsLoading = true;

        TorrentsList.Clear();

        if (_details.Title != null)
        {
            var list = await _nyaaService.SearchAsync(_details.Title, TorrentSearchTerms);
            TorrentsList = new ObservableCollection<NyaaTorrent>(list.OrderByDescending(x => x.Seeders));
        }

        IsTorrentsLoading = false;
    }

    [RelayCommand]
    public void SortBySeeders()
    {
        if (SeedersSortDirection == SortDirection.Descending)
        {
            SeedersSortDirection = SortDirection.Ascending;
            TorrentsList = new ObservableCollection<NyaaTorrent>(TorrentsList.OrderBy(x => x.Seeders));
        }
        else
        {
            SeedersSortDirection = SortDirection.Descending;
            TorrentsList = new ObservableCollection<NyaaTorrent>(TorrentsList.OrderByDescending(x => x.Seeders));
        }
    }

    [RelayCommand]
    public void SortByReleaseDate()
    {
        if (DateSortDirection == SortDirection.Descending)
        {
            DateSortDirection = SortDirection.Ascending;
            TorrentsList = new ObservableCollection<NyaaTorrent>(TorrentsList.OrderBy(x => x.PublishDate));
        }
        else
        {
            DateSortDirection = SortDirection.Descending;
            TorrentsList = new ObservableCollection<NyaaTorrent>(TorrentsList.OrderByDescending(x => x.PublishDate));
        }
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