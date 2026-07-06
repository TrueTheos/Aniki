using System.Collections.ObjectModel;
using System.Globalization;
using Aniki.Services.Anime;
using Aniki.Services.Interfaces;

namespace Aniki.ViewModels;

public partial class CalendarViewModel : ViewModelBase
{
    private readonly Dictionary<DateTime, DaySchedule> _cachedDays = new();
    private DateTime _viewStartDate;

    [ObservableProperty] public partial ObservableCollection<DaySchedule> Days { get; set; }
    [ObservableProperty] public partial bool ShowOnlyMyAnime { get; set; }
    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial string CurrentWeekRange { get; set; } = "";

    partial void OnShowOnlyMyAnimeChanged(bool value)
    {
        RebuildDaySchedules();
    }

    private readonly List<int> _watchingList = [];
    private readonly IAnimeService _animeService;
    private readonly ICalendarService _calendarService;

    private double CurrentTimeOffset
    {
        get
        {
            DateTime now = DateTime.Now;
            int minutesFromMidnight = now.Hour * 60 + now.Minute;
            const double pxPerMinute = 1440.0 / (24 * 60);
            return minutesFromMidnight * pxPerMinute;
        }
    }

    public CalendarViewModel(IAnimeService animeService, ICalendarService calendarService)
    {
        _animeService    = animeService;
        _calendarService = calendarService;
        Days             = [];
        UpdateCurrentWeekRange();
    }

    public override async Task Enter()
    {
        _viewStartDate = GetCurrentWeekStart();
        await LoadWatchingListAsync();
        await FetchAndDisplayScheduleAsync();
    }
    
    private DateTime GetCurrentWeekStart()
        => DateTime.UtcNow.Date.AddDays(-((int)DateTime.UtcNow.Date.DayOfWeek + 6) % 7);
        
    private async Task LoadWatchingListAsync()
    {
        _watchingList.Clear();
        
        if(!AnimeService.IsLoggedIn) return;
        
        var watching = await _animeService.GetUserAnimeListAsync(AnimeStatus.Watching);
        var planToWatch = await _animeService.GetUserAnimeListAsync(AnimeStatus.PlanToWatch);

        foreach (AnimeDetails anime in watching) _watchingList.Add(anime.Id);
        foreach (AnimeDetails anime in planToWatch) _watchingList.Add(anime.Id);
    }

    private async Task FetchAndDisplayScheduleAsync(bool forceRefresh = false)
    {
        IsLoading = true;

        var uncachedDates = Enumerable
            .Range(0, 7)
            .Select(i => _viewStartDate.AddDays(i).Date)
            .Where(d => forceRefresh || !_cachedDays.ContainsKey(d))
            .ToList();

        if (uncachedDates.Count > 0)
        {
            var schedule = await _calendarService.GetScheduleAsync(
                _watchingList,
                uncachedDates.First(),
                uncachedDates.Last().AddDays(1));
 
            foreach (DaySchedule day in schedule)
                _cachedDays[day.Date] = day;
        }
 
        RefreshView();
        IsLoading = false;
    }

    public async Task GoToClickedAnime(AnimeScheduleItem anime)
    {
        if (anime.GetId() is { } id and > 0)
            await DependencyInjection.Instance.ServiceProvider!.GetService<MainViewModel>()!.GoToAnime(id);
        else
            await DependencyInjection.Instance.ServiceProvider!.GetService<MainViewModel>()!.GoToAnime(anime.Title);
    }

    [RelayCommand]
    private async Task GoBackDay()
    {
        _viewStartDate = _viewStartDate.AddDays(-1);
        await FetchAndDisplayScheduleAsync();
    }
        
    [RelayCommand]
    private async Task GoBackWeek()
    {
        _viewStartDate = _viewStartDate.AddDays(-7);
        await FetchAndDisplayScheduleAsync();
    }

    [RelayCommand]
    private async Task GoToStart()
    {
        _viewStartDate = DateTime.UtcNow.Date.AddDays(-((int)DateTime.UtcNow.Date.DayOfWeek + 6) % 7);
        await FetchAndDisplayScheduleAsync();
    }

    [RelayCommand]
    private async Task GoForwardDay()
    {
        _viewStartDate = _viewStartDate.AddDays(1);
        await FetchAndDisplayScheduleAsync();
    }

    [RelayCommand]
    private async Task GoForwardWeek()
    {
        _viewStartDate = _viewStartDate.AddDays(7);
        await FetchAndDisplayScheduleAsync();
    }

    private void RefreshView()
    {
        RebuildDaySchedules();
        UpdateCurrentWeekRange();

        OnPropertyChanged(nameof(CurrentTimeOffset));
    }

    private void RebuildDaySchedules()
    {
        List<DaySchedule> newDays = [];

        for (int i = 0; i < 7; i++)
        {
            DateTime date = _viewStartDate.AddDays(i);
            
            if (_cachedDays.TryGetValue(date.Date, out DaySchedule? cached))
            {
                var filteredItems = cached.Items
                    .Where(item => !ShowOnlyMyAnime || (item.GetId() is {} id && _watchingList.Contains(id)));

                foreach (AnimeScheduleItem item in filteredItems)
                    item.IsAiringNow = IsCurrentlyAiring(item.AiringAt, date);

                newDays.Add(new DaySchedule
                {
                    DayName     = cached.DayName,
                    Date        = cached.Date,
                    IsToday     = cached.IsToday,
                    ColumnIndex = i,
                    Items       = new ObservableCollection<AnimeScheduleItem>(filteredItems)
                });
            }
            else
            {
                newDays.Add(new DaySchedule
                {
                    DayName     = date.ToString("dddd", CultureInfo.InvariantCulture),
                    Date        = date,
                    IsToday     = date.Date == DateTime.Today,
                    ColumnIndex = i,
                    Items       = new()
                });
            }
        }

        Days.Clear();
        foreach (DaySchedule day in newDays)
        {
            Days.Add(day);
        }
    }

    private bool IsCurrentlyAiring(DateTime airingTime, DateTime dayDate)
    {
        if (dayDate.Date != DateTime.Today) return false;

        DateTime airingDateTime = dayDate.Date.Add(airingTime.TimeOfDay);
        return Math.Abs((DateTime.Now - airingDateTime).TotalMinutes) <= 30;
    }

    private void UpdateCurrentWeekRange()
    {
        DateTime endDate = _viewStartDate.AddDays(6);
        CurrentWeekRange = $"{_viewStartDate:MMM d} - {endDate:MMM d, yyyy}";
    }
}