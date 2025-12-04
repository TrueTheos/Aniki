namespace Aniki.ViewModels;

public class WatchAnimeViewModel : ViewModelBase
{
    public int SelectedTabIndex
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
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

    public void GoToAnimeInOnlineView(int malId, string title)
    {
        SelectedTabIndex = 1;
        _ = OnlineViewModel.GoToAnime(malId, title);
    }
}