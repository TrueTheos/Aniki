using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Aniki.ViewModels;

public partial class WatchAnimeViewModel : ViewModelBase
{
    private int _selectedTabIndex;
    
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (_selectedTabIndex != value)
            {
                _selectedTabIndex = value;
                OnPropertyChanged();
                
                if (value == 0)
                    _ = DownloadedViewModel.Enter();
                else if (value == 1)
                    _ = OnlineViewModel.Enter();
            }
        }
    }
    
    public DownloadedViewModel DownloadedViewModel { get; }
    public OnlineViewModel OnlineViewModel { get; }
    
    public WatchAnimeViewModel(DownloadedViewModel downloadedViewModel, OnlineViewModel onlineViewModel)
    {
        DownloadedViewModel = downloadedViewModel;
        OnlineViewModel = onlineViewModel;
    }
}