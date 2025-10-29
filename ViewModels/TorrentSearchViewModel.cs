using Aniki.Models;
using Aniki.Services.Interfaces;
using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aniki.Models.MAL;

namespace Aniki.ViewModels;

public partial class TorrentSearchViewModel : ViewModelBase
{
    [ObservableProperty] private bool _isTorrentsLoading;
    [ObservableProperty] private string _torrentSearchTerms = string.Empty;
    [ObservableProperty] private ObservableCollection<NyaaTorrent> _torrentsList = new();
    
    private AnimeFieldSet? _details;

    private readonly INyaaService _nyaaService;

    public TorrentSearchViewModel(INyaaService nyaaService)
    {
        _nyaaService = nyaaService;
    }

    public void Update(AnimeFieldSet? details, int episodesWatched)
    {
        _details = details;
        
        TorrentsList.Clear();
    }

    [RelayCommand]
    public async Task SearchTorrents()
    {
        if (_details == null) return;
        
        IsTorrentsLoading = true;

        TorrentsList.Clear();

        if (_details.Title != null)
        {
            List<NyaaTorrent> list = await _nyaaService.SearchAsync(_details.Title, TorrentSearchTerms);

            foreach (NyaaTorrent t in list) 
            {
                TorrentsList.Add(t);
            }
        }

        IsTorrentsLoading = false;
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
