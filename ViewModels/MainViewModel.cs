using Aniki.Misc;
using Avalonia.Media.Imaging;
using System.Collections.ObjectModel;
using Aniki.Services.Interfaces;

namespace Aniki.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private string? _username;

    [ObservableProperty]
    private Bitmap? _profileImage;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

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
    #endregion

    [ObservableProperty]
    private ObservableCollection<AnimeScheduleItem> _todayAnime = new();
    
    private readonly ICalendarService _calendarService;
    private readonly IMalService _malService;

    public MainViewModel(ICalendarService calendarService, IMalService malService, AnimeDetailsViewModel animeDetailsViewModel,
        WatchAnimeViewModel watchViewModel, CalendarViewModel calendarViewModel, StatsViewModel statsViewModel, AnimeBrowseViewModel animeBrowseViewModel) 
    {
        _calendarService = calendarService;
        _malService = malService;
        _animeDetailsViewModel = animeDetailsViewModel;
        _watchViewModel = watchViewModel;
        _calendarViewModel = calendarViewModel;
        _statsViewModel = statsViewModel;
        _animeBrowseViewModel = animeBrowseViewModel;
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

    public void GoToAnime(string title)
    {
        _ = SearchForAnime(title.Replace('-', ' '));
    }

    public void GoToAnime(int malId)
    {
        _ = SearchForAnime(malId);
    }

    public async Task InitializeAsync()
    {
        await LoadUserDataAsync();
        await LoadTodayAnimeAsync();
    }

    private async Task LoadTodayAnimeAsync()
    {
        var watchingList = await _malService.GetUserAnimeList(AnimeStatusApi.watching);
        var animes = await _calendarService.GetAnimeScheduleForDayAsync(DateTime.Today, watchingList.Select(x => x.Node.Title).ToList());

        TodayAnime.Clear();
        foreach (AnimeScheduleItem anime in animes)
        {
            if(watchingList.Any(x => x.Node.Id == anime.MalId))
                TodayAnime.Add(anime);
        }
    }

    private async Task LoadUserDataAsync()
    {
        try
        {
            IsLoading = true;
            MAL_UserData malUserData = await _malService.GetUserDataAsync();
            Username = malUserData.Name;
            ProfileImage = await _malService.GetUserPicture();
        }
        catch (Exception ex)
        {
            Log.Information($"Error loading user data: {ex.Message}");
        }
        finally
        {
            _ = ShowAnimeBrowsePage();
            IsLoading = false;
        }
    }
        
    [RelayCommand]
    private void Logout()
    {
        LogoutRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public async Task Search()
    {
        await SearchForAnime(SearchQuery);
    }

    private async Task SearchForAnime(string searchQuery)
    {
        IsLoading = true;

        _ = ShowAnimeBrowsePage();

        try
        {
            await AnimeBrowseViewModel.SearchAnimeByTitle(searchQuery);
        }
        catch (Exception ex)
        {
            Log.Information($"Error searching: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SearchForAnime(int malId)
    {
        IsLoading = true;

        _ = ShowAnimeDetailsPage();

        try
        {
            await AnimeDetailsViewModel.SearchAnimeById(malId);
        }
        catch (Exception ex)
        {
            Log.Information($"Error searching: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }
}