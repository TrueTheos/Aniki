using System.Collections.Generic;
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

    [ObservableProperty] private bool _isMainPageNavSelected;
    [ObservableProperty] private bool _isAnimeDetailsNavSelected;
    [ObservableProperty] private bool _isWatchNavSelected;
    [ObservableProperty] private bool _isCalendarNavSelected;
    [ObservableProperty] private bool _isStatsNavSelected;
    [ObservableProperty] private bool _isLibraryNavSelected;

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

    private readonly Stack<ViewModelBase> _navigationBackStack = new();
    private readonly Stack<ViewModelBase> _navigationForwardStack = new();

    [ObservableProperty]
    private bool _canNavigateBack;

    [ObservableProperty]
    private bool _canNavigateForward;
    
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

    partial void OnCurrentViewModelChanged(ViewModelBase? value)
    {
        IsMainPageNavSelected = ReferenceEquals(value, AnimeBrowseViewModel);
        IsAnimeDetailsNavSelected = ReferenceEquals(value, AnimeDetailsViewModel);
        IsWatchNavSelected = ReferenceEquals(value, WatchViewModel);
        IsCalendarNavSelected = ReferenceEquals(value, CalendarViewModel);
        IsStatsNavSelected = ReferenceEquals(value, StatsViewModel);
        IsLibraryNavSelected = ReferenceEquals(value, UserAnimeListViewModel);
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
    public async Task NavigateBackAsync()
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
    public async Task NavigateForwardAsync()
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
    public async Task ShowAnimeBrowsePage()
    {
        await NavigateToViewAsync(AnimeBrowseViewModel);
    }

    [RelayCommand]
    public async Task ShowAnimeDetailsPage()
    {
        await NavigateToViewAsync(AnimeDetailsViewModel);
    }

    [RelayCommand]
    public async Task ShowWatchPage()
    {
        await NavigateToViewAsync(WatchViewModel);
    }

    [RelayCommand]
    public async Task ShowCalendarPage()
    {
        await NavigateToViewAsync(CalendarViewModel);
    }

    [RelayCommand]
    public async Task ShowStatsPage()
    {
        await NavigateToViewAsync(StatsViewModel);
    }
    
    [RelayCommand]
    public async Task ShowUserAnimeListPage()
    {
        await NavigateToViewAsync(UserAnimeListViewModel);
    }
    
    [RelayCommand]
    public async Task ShowReadPage()
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