using Aniki.Services.Anime;
using Aniki.Services.Interfaces;

namespace Aniki.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private string? _username;

    [ObservableProperty]
    private bool _isLoading;

    public event EventHandler? LogoutRequested;
    public event EventHandler? SettingsRequested;

    #region Views
    [ObservableProperty]
    private ViewModelBase? _currentViewModel;

    [ObservableProperty] private AnimeDetailsViewModel _animeDetailsViewModel;
    [ObservableProperty]
    private WatchAnimeViewModel _watchViewModel;
    [ObservableProperty]
    private CalendarViewModel _calendarViewModel;
    [ObservableProperty]
    private StatsViewModel _statsViewModel;
    [ObservableProperty]
    private AnimeBrowseViewModel _animeBrowseViewModel;
    [ObservableProperty]
    private UserAnimeListViewModel _userAnimeListViewModel;
    [ObservableProperty]
    private ReadViewModel _readViewMode;
    #endregion

    private readonly IAnimeService _animeService;
    private readonly IVideoPlayerService _videoPlayerService;
    
    public MainViewModel(IAnimeService animeService, AnimeDetailsViewModel animeDetailsViewModel,
        WatchAnimeViewModel watchViewModel, CalendarViewModel calendarViewModel, StatsViewModel statsViewModel, AnimeBrowseViewModel animeBrowseViewModel,
        UserAnimeListViewModel userAnimeListViewModel, ReadViewModel readViewModel, IVideoPlayerService videoPlayerService) 
    {
        _animeService           = animeService;
        _animeDetailsViewModel  = animeDetailsViewModel;
        _watchViewModel         = watchViewModel;
        _calendarViewModel      = calendarViewModel;
        _statsViewModel         = statsViewModel;
        _animeBrowseViewModel   = animeBrowseViewModel;
        _userAnimeListViewModel = userAnimeListViewModel;
        _videoPlayerService     = videoPlayerService;
        _readViewMode           = readViewModel;
        
        _ = AnimeDetailsViewModel.LoadAnimeDetailsAsync(1);
    }

    [RelayCommand]
    public async Task ShowAnimeBrowsePage()
    {
        CurrentViewModel = AnimeBrowseViewModel;
        await CurrentViewModel.Enter();
    }

    [RelayCommand]
    public async Task ShowAnimeDetailsPage()
    {
        CurrentViewModel = AnimeDetailsViewModel;
        await CurrentViewModel.Enter();
    }

    [RelayCommand]
    public async Task ShowWatchPage()
    {
        CurrentViewModel = WatchViewModel;
        await CurrentViewModel.Enter();
    }

    [RelayCommand]
    public async Task ShowCalendarPage()
    {
        CurrentViewModel = CalendarViewModel; 
        await CurrentViewModel.Enter();
    }

    [RelayCommand]
    public async Task ShowStatsPage()
    {
        CurrentViewModel = StatsViewModel; 
        await CurrentViewModel.Enter();
    }
    
    [RelayCommand]
    public async Task ShowUserAnimeListPage()
    {
        CurrentViewModel = UserAnimeListViewModel; 
        await CurrentViewModel.Enter();
    }
    
    [RelayCommand]
    public async Task ShowReadPage()
    {
        CurrentViewModel = ReadViewMode; 
        await CurrentViewModel.Enter();
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
        await _videoPlayerService.RefreshPlayersAsync();
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