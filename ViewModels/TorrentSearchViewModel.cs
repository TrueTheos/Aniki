using Aniki.Models;
using Aniki.Services.Interfaces;
using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Aniki.Models.MAL;

namespace Aniki.ViewModels;

public partial class TorrentSearchViewModel : ViewModelBase
{
    [ObservableProperty] private bool _isTorrentsLoading;
    [ObservableProperty] private string _torrentSearchTerms = string.Empty;
    [ObservableProperty] private ObservableCollection<NyaaTorrent> _torrentsList = new();
    
    private AnimeFieldSet? _details;
    private List<NyaaTorrent> _cachedTorrents = new();

    private readonly INyaaService _nyaaService;

    public TorrentSearchViewModel(INyaaService nyaaService)
    {
        _nyaaService = nyaaService;
    }

    public void Update(AnimeFieldSet? details, int episodesWatched)
    {
        _details = details;
        
        TorrentsList.Clear();
        _cachedTorrents.Clear();
    }

    [RelayCommand]
    public async Task SearchTorrents()
    {
        if (_details == null) return;
        
        IsTorrentsLoading = true;

        TorrentsList.Clear();
        _cachedTorrents.Clear();

        if (_details.Title != null)
        {
            List<NyaaTorrent> list = await _nyaaService.SearchAsync(_details.Title, TorrentSearchTerms);

            _cachedTorrents = new List<NyaaTorrent>(list);

            foreach (NyaaTorrent t in list) 
            {
                TorrentsList.Add(t);
            }
        }

        IsTorrentsLoading = false;
    }

    [RelayCommand]
    public void SortBySeeders()
    {
        var sorted = _cachedTorrents.OrderByDescending(t => t.Seeders).ToList();
        
        TorrentsList.Clear();
        foreach (var torrent in sorted)
        {
            TorrentsList.Add(torrent);
        }
        
        _cachedTorrents = sorted;
    }

    [RelayCommand]
    public void SortByReleaseDate()
    {
        var sorted = _cachedTorrents.OrderByDescending(t => t.PublishDate).ToList();
        
        TorrentsList.Clear();
        foreach (var torrent in sorted)
        {
            TorrentsList.Add(torrent);
        }
        
        _cachedTorrents = sorted;
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