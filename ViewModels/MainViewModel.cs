using Aniki.Services.Anime;
using Aniki.Services.Interfaces;

namespace Aniki.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty] public partial string? Username { get; set; }
    [ObservableProperty] public partial bool IsLoading { get; set; }

    [ObservableProperty] public partial ViewModelBase? CurrentViewModel { get; set; }

    [ObservableProperty] public partial bool IsMainPageNavSelected { get; set; }
    [ObservableProperty] public partial bool IsAnimeDetailsNavSelected { get; set; }
    [ObservableProperty] public partial bool IsWatchNavSelected { get; set; }
    [ObservableProperty] public partial bool IsCalendarNavSelected { get; set; }
    [ObservableProperty] public partial bool IsStatsNavSelected { get; set; }
    [ObservableProperty] public partial bool IsLibraryNavSelected { get; set; }

    [ObservableProperty] public partial AnimeDetailsViewModel AnimeDetailsViewModel { get; set; }
    [ObservableProperty] public partial WatchAnimeViewModel WatchViewModel { get; set; }
    [ObservableProperty] public partial CalendarViewModel CalendarViewModel { get; set; }
    [ObservableProperty] public partial StatsViewModel StatsViewModel { get; set; }
    [ObservableProperty] public partial AnimeBrowseViewModel AnimeBrowseViewModel { get; set; }
    [ObservableProperty] public partial UserAnimeListViewModel UserAnimeListViewModel { get; set; }
    [ObservableProperty] public partial ReadViewModel ReadViewMode { get; set; }
    
    [ObservableProperty] public partial bool CanNavigateBack { get; set; }
    [ObservableProperty] public partial bool CanNavigateForward { get; set; }

    public event EventHandler? LogoutRequested;
    public event EventHandler? SettingsRequested;
    
    private readonly IAnimeService _animeService;
    private readonly IVideoPlayerService _videoPlayerService;

    private readonly Stack<ViewModelBase> _navigationBackStack = new();
    private readonly Stack<ViewModelBase> _navigationForwardStack = new();

    public MainViewModel(IAnimeService animeService, AnimeDetailsViewModel animeDetailsViewModel,
        WatchAnimeViewModel watchViewModel, CalendarViewModel calendarViewModel, StatsViewModel statsViewModel, AnimeBrowseViewModel animeBrowseViewModel,
        UserAnimeListViewModel userAnimeListViewModel, ReadViewModel readViewModel, IVideoPlayerService videoPlayerService) 
    {
        _animeService           = animeService;
        AnimeDetailsViewModel = animeDetailsViewModel;
        WatchViewModel = watchViewModel;
        CalendarViewModel = calendarViewModel;
        StatsViewModel = statsViewModel;
        AnimeBrowseViewModel = animeBrowseViewModel;
        UserAnimeListViewModel = userAnimeListViewModel;
        _videoPlayerService     = videoPlayerService;
        ReadViewMode = readViewModel;
        
        _ = AnimeDetailsViewModel.LoadAnimeDetailsAsync(1);
    }
    
    private void RefreshNavigationAvailability()
    {
        CanNavigateBack = _navigationBackStack.Count > 0;
        CanNavigateForward = _navigationForwardStack.Count > 0;
    }

    private async Task NavigateToViewAsync(ViewModelBase target)
    {
        if (ReferenceEquals(CurrentViewModel, target))
        {
            await target.Enter();
            return;
        }

        if (CurrentViewModel is not null)
        {
            _navigationBackStack.Push(CurrentViewModel);
            _navigationForwardStack.Clear();
        }

        CurrentViewModel = target;
        RefreshNavigationAvailability();
        await CurrentViewModel!.Enter();
    }

    [RelayCommand]
    private async Task NavigateBackAsync()
    {
        if (_navigationBackStack.Count == 0 || CurrentViewModel is null)
        {
            return;
        }

        ViewModelBase previous = _navigationBackStack.Pop();
        _navigationForwardStack.Push(CurrentViewModel);
        CurrentViewModel = previous;
        RefreshNavigationAvailability();
        await CurrentViewModel.Enter();
    }

    [RelayCommand]
    private async Task NavigateForwardAsync()
    {
        if (_navigationForwardStack.Count == 0 || CurrentViewModel is null)
        {
            return;
        }

        ViewModelBase next = _navigationForwardStack.Pop();
        _navigationBackStack.Push(CurrentViewModel);
        CurrentViewModel = next;
        RefreshNavigationAvailability();
        await CurrentViewModel.Enter();
    }

    [RelayCommand]
    private async Task ShowAnimeBrowsePage()
    {
        await NavigateToViewAsync(AnimeBrowseViewModel);
    }

    [RelayCommand]
    private async Task ShowAnimeDetailsPage()
    {
        await NavigateToViewAsync(AnimeDetailsViewModel);
    }

    [RelayCommand]
    public async Task ShowWatchPage()
    {
        await NavigateToViewAsync(WatchViewModel);
    }

    [RelayCommand]
    private async Task ShowCalendarPage()
    {
        await NavigateToViewAsync(CalendarViewModel);
    }

    [RelayCommand]
    private async Task ShowStatsPage()
    {
        await NavigateToViewAsync(StatsViewModel);
    }
    
    [RelayCommand]
    private async Task ShowUserAnimeListPage()
    {
        await NavigateToViewAsync(UserAnimeListViewModel);
    }
    
    [RelayCommand]
    private async Task ShowReadPage()
    {
        await NavigateToViewAsync(ReadViewMode);
    }

    public async Task GoToAnime(string title)
    {
        await SearchForAnime(title.Replace('-', ' '));
    }

    public async Task GoToAnime(int malId)
    {
       await SearchForAnime(malId);
    }

    public async Task InitializeAsync()
    {
        await LoadUserDataAsync();
        await _videoPlayerService.ScanPlayersAsync();
    }

    private async Task LoadUserDataAsync()
    {
        if (AnimeService.IsLoggedIn)
        {
            IsLoading = true;
            UserData malUserData = await _animeService.GetUserDataAsync();
            Username = malUserData.Name;
        }
        
        _ = ShowAnimeBrowsePage();
        IsLoading = false;
    }
        
    [RelayCommand]
    private void Logout()
    {
        LogoutRequested?.Invoke(this, EventArgs.Empty);
    }

    private async Task SearchForAnime(string searchQuery)
    {
        IsLoading = true;

        await ShowAnimeBrowsePage();

        await AnimeBrowseViewModel.SearchAnimeByTitle(searchQuery);
        IsLoading = false;
    }

    private async Task SearchForAnime(int malId)
    {
        IsLoading = true;

        await ShowAnimeDetailsPage();
        
        await AnimeDetailsViewModel.LoadAnimeDetailsAsync(malId);
        IsLoading = false;
    }

    [RelayCommand]
    private void OpenSettings()
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }
}