using Aniki.Services.Anime;
using Aniki.Services.Anime.Providers;
using Aniki.Services.Auth;
using Aniki.Services.Interfaces;

namespace Aniki.ViewModels;

internal sealed partial class MainViewModel : ViewModelBase
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
    [ObservableProperty] public partial ExploreViewModel ExploreViewModel { get; set; }
    
    [ObservableProperty] public partial bool CanNavigateBack { get; set; }
    [ObservableProperty] public partial bool CanNavigateForward { get; set; }

    public event EventHandler? LogoutRequested;
    public event EventHandler? SettingsRequested;
    
    private readonly IAnimeService _animeService;
    private readonly IVideoPlayerService _videoPlayerService;
    private readonly AnilistService _anilistService;
    private readonly EpisodeNotifier _episodeNotifier;

    private readonly Stack<ViewModelBase> _navigationBackStack = new();
    private readonly Stack<ViewModelBase> _navigationForwardStack = new();

    public MainViewModel(IAnimeService animeService, AnimeDetailsViewModel animeDetailsViewModel,
        WatchAnimeViewModel watchViewModel, CalendarViewModel calendarViewModel, StatsViewModel statsViewModel, AnimeBrowseViewModel animeBrowseViewModel,
        UserAnimeListViewModel userAnimeListViewModel, ReadViewModel readViewModel, IVideoPlayerService videoPlayerService,
        ExploreViewModel exploreViewModel, AnilistService anilistService, EpisodeNotifier episodeNotifier) 
    {
        _episodeNotifier = episodeNotifier;
        ExploreViewModel = exploreViewModel;
        _anilistService = anilistService;
        _animeService           = animeService;
        AnimeDetailsViewModel = animeDetailsViewModel;
        WatchViewModel = watchViewModel;
        CalendarViewModel = calendarViewModel;
        StatsViewModel = statsViewModel;
        AnimeBrowseViewModel = animeBrowseViewModel;
        UserAnimeListViewModel = userAnimeListViewModel;
        _videoPlayerService     = videoPlayerService;
        ReadViewMode = readViewModel;
    }
    
    partial void OnCurrentViewModelChanged(ViewModelBase? value)
    {
        if (value != ReadViewMode && ReadViewMode is { IsFullscreen: true }) ReadViewMode.IsFullscreen = false;
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
            await target.Enter().ConfigureAwait(true);
            return;
        }

        if (CurrentViewModel is not null)
        {
            _navigationBackStack.Push(CurrentViewModel);
            _navigationForwardStack.Clear();
        }

        CurrentViewModel = target;
        RefreshNavigationAvailability();
        await CurrentViewModel!.Enter().ConfigureAwait(true);
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
        await CurrentViewModel.Enter().ConfigureAwait(true);
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
        await CurrentViewModel.Enter().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ShowAnimeBrowsePage()
    {
        await NavigateToViewAsync(AnimeBrowseViewModel).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ShowAnimeDetailsPage()
    {
        await NavigateToViewAsync(AnimeDetailsViewModel).ConfigureAwait(true);
    }

    [RelayCommand]
    public async Task ShowWatchPage()
    {
        await NavigateToViewAsync(WatchViewModel).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ShowCalendarPage()
    {
        await NavigateToViewAsync(CalendarViewModel).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ShowStatsPage()
    {
        await NavigateToViewAsync(StatsViewModel).ConfigureAwait(true);
    }
    
    [RelayCommand]
    private async Task ShowUserAnimeListPage()
    {
        await NavigateToViewAsync(UserAnimeListViewModel).ConfigureAwait(true);
    }
    
    [RelayCommand]
    private async Task ShowReadPage()
    {
        await NavigateToViewAsync(ReadViewMode).ConfigureAwait(true);
    }

    public async Task OpenInReader(AnimeDetails manga)
    {
        await NavigateToViewAsync(ReadViewMode).ConfigureAwait(true);
        await ReadViewMode.OpenAsync(manga).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task SetMediaKind(string kindName)
    {
        MediaKind kind = Enum.Parse<MediaKind>(kindName);
        if (MediaMode.Instance.Kind == kind) return;
        SwitchKind(kind);

        ViewModelBase target = CurrentViewModel == UserAnimeListViewModel || CurrentViewModel == StatsViewModel || CurrentViewModel == ExploreViewModel
            ? CurrentViewModel
            : AnimeBrowseViewModel;
        CurrentViewModel = null;
        await NavigateToViewAsync(target).ConfigureAwait(true);
    }

    private void SwitchKind(MediaKind kind)
    {
        MediaMode.Instance.Kind = kind;
        _navigationBackStack.Clear();
        _navigationForwardStack.Clear();
        RefreshNavigationAvailability();
    }

    [RelayCommand]
    private async Task ShowExplorePage()
    {
        await NavigateToViewAsync(ExploreViewModel).ConfigureAwait(true);
    }

    public async Task ShowExploreGenre(string genre)
    {
        ExploreViewModel.PresetGenre(genre);
        await NavigateToViewAsync(ExploreViewModel).ConfigureAwait(true);
    }

    public async Task OpenSourceManga(AnimeDetails anime)
    {
        int? mangaId = await _anilistService.GetSourceMangaIdAsync(anime.Id, AnimeService.CurrentProviderType == ILoginProvider.ProviderType.Mal).ConfigureAwait(true);
        if (mangaId == null && anime.Title != null)
            mangaId = (await _animeService.Manga.SearchAnimeAsync(anime.Title).ConfigureAwait(true)).FirstOrDefault()?.Id;
        if (mangaId == null)
        {
            await ToastService.Show($"Couldn't find a manga for {anime.Title}").ConfigureAwait(true);
            return;
        }

        SwitchKind(MediaKind.Manga);
        await SearchForAnime(mangaId.Value).ConfigureAwait(true);
    }

    public async Task GoToAnime(string title)
    {
        MediaMode.Instance.Kind = MediaKind.Anime;
        await SearchForAnime(title.Replace('-', ' ')).ConfigureAwait(true);
    }

    public async Task GoToAnime(int malId)
    {
        MediaMode.Instance.Kind = MediaKind.Anime;
        await SearchForAnime(malId).ConfigureAwait(true);
    }

    public async Task GoToMedia(int id)
    {
       await SearchForAnime(id).ConfigureAwait(true);
    }

    public async Task InitializeAsync()
    {
        await LoadUserDataAsync().ConfigureAwait(true);
        _ = _episodeNotifier.RunAsync();
        await _videoPlayerService.ScanPlayersAsync().ConfigureAwait(true);
    }

    private async Task LoadUserDataAsync()
    {
        if (AnimeService.IsLoggedIn)
        {
            IsLoading = true;
            UserData malUserData = await _animeService.GetUserDataAsync().ConfigureAwait(true);
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

        await ShowAnimeBrowsePage().ConfigureAwait(true);

        await AnimeBrowseViewModel.SearchAnimeByTitle(searchQuery).ConfigureAwait(true);
        IsLoading = false;
    }

    private async Task SearchForAnime(int malId)
    {
        IsLoading = true;

        await ShowAnimeDetailsPage().ConfigureAwait(true);
        
        await AnimeDetailsViewModel.LoadAnimeDetailsAsync(malId).ConfigureAwait(true);
        IsLoading = false;
    }

    [RelayCommand]
    private void OpenSettings()
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }
}