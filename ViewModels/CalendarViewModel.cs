using System.Collections.ObjectModel;
using System.Globalization;
using Aniki.Services.Anime;
using Aniki.Services.Auth;
using Aniki.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Aniki.ViewModels;

public partial class CalendarViewModel : ViewModelBase
{
    private Dictionary<DateTime, DaySchedule> _cachedDays = new();
    private DateTime _windowStartDate;

    [ObservableProperty]
    private ObservableCollection<DaySchedule> _days;

    [ObservableProperty]
    private bool _showOnlyMyAnime;

    partial void OnShowOnlyMyAnimeChanged(bool value)
    {
        ShowWindow();
    }

    private readonly List<int> _watchingList = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _currentWeekRange = "";

    private readonly IAnimeService _animeService;
    private readonly ICalendarService _calendarService;

    public double CurrentTimeOffset
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
        _animeService = animeService;
        _calendarService = calendarService;
        Days = new();
        UpdateCurrentWeekRange();
    }

    public override async Task Enter()
    {
        _windowStartDate = DateTime.UtcNow.Date.AddDays(-((int)DateTime.UtcNow.Date.DayOfWeek + 6) % 7);
        await LoadUserAnimeList();
        await LoadScheduleAsync();
    }
        
    private async Task LoadUserAnimeList()
    {
        _watchingList.Clear();
        
        if(!AnimeService.IsLoggedIn) return;
        
        List<AnimeDetails> watching = await _animeService.GetUserAnimeListAsync(AnimeStatus.Watching);
        List<AnimeDetails> planToWatch = await _animeService.GetUserAnimeListAsync(AnimeStatus.PlanToWatch);

        foreach (AnimeDetails anime in watching)
        {
            _watchingList.Add(anime.Id);
        }
        foreach (AnimeDetails anime in planToWatch)
        {
            _watchingList.Add(anime.Id);
        }
    }

    private async Task LoadScheduleAsync(bool forceRefresh = false)
    {
        IsLoading = true;

        DateTime startDate = _windowStartDate;
        DateTime endDate = _windowStartDate.AddDays(7);

        List<DateTime> daysToFetch = new();
        for (DateTime date = startDate; date < endDate; date = date.AddDays(1))
        {
            if (forceRefresh || !_cachedDays.ContainsKey(date.Date))
            {
                daysToFetch.Add(date.Date);
            }
        }

        if (daysToFetch.Any())
        {
            List<DaySchedule> schedule = await _calendarService.GetScheduleAsync(_watchingList, daysToFetch.First(), daysToFetch.Last().AddDays(1));
            foreach (DaySchedule day in schedule)
            {
                _cachedDays[day.Date] = day;
            }
        }

        await ShowWindowAsync();
                
        IsLoading = false;
    }

    public async Task GoToClickedAnime(AnimeScheduleItem anime)
    {
        MainViewModel vm = DependencyInjection.Instance.ServiceProvider!.GetRequiredService<MainViewModel>();
        if (anime.GetId() != null && anime.GetId()!.Value > 0)
        {
            await vm.GoToAnime(anime.GetId()!.Value);
        }
        else
        {
            await vm.GoToAnime(anime.Title);
        }
    }

    [RelayCommand]
    private async Task GoBackDay()
    {
        _windowStartDate = _windowStartDate.AddDays(-1);
        await LoadScheduleAsync();
    }
        
    [RelayCommand]
    public async Task GoBackWeek()
    {
        _windowStartDate = _windowStartDate.AddDays(-7);
        await LoadScheduleAsync();
    }

    [RelayCommand]
    private async Task GoToStart()
    {
        _windowStartDate = DateTime.UtcNow.Date.AddDays(-((int)DateTime.UtcNow.Date.DayOfWeek + 6) % 7);
        await LoadScheduleAsync(true);
    }

    [RelayCommand]
    private async Task GoForwardDay()
    {
        _windowStartDate = _windowStartDate.AddDays(1);
        await LoadScheduleAsync();
    }

    [RelayCommand]
    private async Task GoForwardWeek()
    {
        _windowStartDate = _windowStartDate.AddDays(7);
        await LoadScheduleAsync();
    }

    private Task ShowWindowAsync()
    {
        ShowWindow();
        UpdateCurrentWeekRange();

        OnPropertyChanged(nameof(CurrentTimeOffset));
        return Task.CompletedTask;
    }

    private void ShowWindow()
    {
        List<DaySchedule> newDays = new();

        for (int i = 0; i < 7; i++)
        {
            DateTime currentDate = _windowStartDate.AddDays(i);
            if (_cachedDays.TryGetValue(currentDate.Date, out DaySchedule? cachedDaySchedule))
            {
                IEnumerable<AnimeScheduleItem> filteredItems = cachedDaySchedule.Items
                    .Where(item => !ShowOnlyMyAnime || (item.GetId() is {} id && _watchingList.Contains(id)))
                    .Select(item => EnhanceAnimeItem(item, currentDate));

                DaySchedule displayDaySchedule = new()
                {
                    Name = cachedDaySchedule.Name,
                    DayName = cachedDaySchedule.DayName,
                    Date = cachedDaySchedule.Date,
                    IsToday = cachedDaySchedule.IsToday,
                    ColumnIndex = i,
                    Items = new ObservableCollection<AnimeScheduleItem>(filteredItems)
                };
                newDays.Add(displayDaySchedule);
            }
            else
            {
                newDays.Add(new()
                {
#pragma warning disable CS0618 // Type or member is obsolete
                    Name = currentDate.DayOfWeek.ToString(CultureInfo.InvariantCulture),
#pragma warning restore CS0618 // Type or member is obsolete
                    DayName = currentDate.ToString("dddd", CultureInfo.InvariantCulture),
                    Date = currentDate,
                    IsToday = currentDate.Date == DateTime.Today,
                    ColumnIndex = i,
                    Items = new()
                });
            }
        }

        Days.Clear();
        foreach (DaySchedule day in newDays)
        {
            Days.Add(day);
        }
    }

    private AnimeScheduleItem EnhanceAnimeItem(AnimeScheduleItem original, DateTime dayDate)
    {
        original.IsAiringNow = IsCurrentlyAiring(original.AiringAt, dayDate);
        return original;
    }

    private bool IsCurrentlyAiring(DateTime airingTime, DateTime dayDate)
    {
        if (dayDate.Date != DateTime.Today) return false;

        DateTime now = DateTime.Now;
        DateTime airingDateTime = dayDate.Date.Add(airingTime.TimeOfDay);

        return Math.Abs((now - airingDateTime).TotalMinutes) <= 30;
    }

    private void UpdateCurrentWeekRange()
    {
        DateTime endDate = _windowStartDate.AddDays(6);
        CurrentWeekRange = $"{_windowStartDate:MMM d} - {endDate:MMM d, yyyy}";
    }
}