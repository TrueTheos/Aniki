namespace Aniki.ViewModels;

internal sealed class WatchAnimeViewModel : ViewModelBase
{
    public int SelectedTabIndex
    {
        get;
        set
        {
            if (field == value) return;

            field = value;
            OnPropertyChanged();
            _ = EnterActiveTabAsync();
        }
    }

    public DownloadedViewModel DownloadedViewModel { get; }
    public OnlineViewModel OnlineViewModel { get; }
    
    public WatchAnimeViewModel(DownloadedViewModel downloadedViewModel, OnlineViewModel onlineViewModel)
    {
        DownloadedViewModel = downloadedViewModel;
        OnlineViewModel = onlineViewModel;
    }

    public override Task Enter() => EnterActiveTabAsync();

    private Task EnterActiveTabAsync() => SelectedTabIndex switch
    {
        0 => DownloadedViewModel.Enter(),
        1 => OnlineViewModel.Enter(),
        _ => Task.CompletedTask
    };

    public void GoToAnimeInOnlineView(int malId, string title)
    {
        SelectedTabIndex = 1;
        _ = OnlineViewModel.GoToAnime(malId, title);
    }
}