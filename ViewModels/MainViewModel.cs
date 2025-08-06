using Aniki.Misc;
using Avalonia.Media.Imaging;
using System.Collections.ObjectModel;

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

    [ObservableProperty]
    private AnimeDetailsViewModel _animeDetailsViewModel = new();
    [ObservableProperty]
    private WatchAnimeViewModel _watchViewModel = new();
    [ObservableProperty]
    private CalendarViewModel _calendarViewModel;
    [ObservableProperty]
    private StatsViewModel _statsViewModel = new();
    #endregion

    [ObservableProperty]
    private ObservableCollection<AnimeScheduleItem> _todayAnime = new();

    public MainViewModel() 
    {
        CalendarViewModel = new(this);
    }

    [RelayCommand]
    public async Task ShowMainPage()
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
        _ = SearchForAnime(title.Replace('-', ' '), true);
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
        var watchingList = await MalUtils.LoadAnimeList(AnimeStatusApi.watching);
        var animes = await CalendarService.GetAnimeScheduleForDayAsync(DateTime.Today, watchingList.Select(x => x.Node.Title).ToList());

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
            UserData userData = await MalUtils.GetUserDataAsync();
            Username = userData.Name;
            ProfileImage = await MalUtils.GetUserPicture();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading user data: {ex.Message}");
        }
        finally
        {
            _ = ShowMainPage();
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

    private async Task SearchForAnime(string searchQuery, bool showFirstBest = false)
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            await AnimeDetailsViewModel.LoadAnimeListAsync(AnimeStatusTranslated.All);
            return;
        }

        IsLoading = true;

        _ = ShowMainPage();

        try
        {
            await AnimeDetailsViewModel.SearchAnimeByTitle(searchQuery, true, showFirstBest);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error searching: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SearchForAnime(int malId)
    {
        IsLoading = true;

        _ = ShowMainPage();

        try
        {
            await AnimeDetailsViewModel.SearchAnimeById(malId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error searching: {ex.Message}");
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