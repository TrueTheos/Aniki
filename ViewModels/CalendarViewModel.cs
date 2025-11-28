using System.Collections.ObjectModel;
using System.Globalization;
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

    private readonly List<string> _watchingList = new() { };

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _currentWeekRange = "";

    private readonly IMalService _malService;
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

    public CalendarViewModel(IMalService malService, ICalendarService calendarService)
    {
        _malService = malService;
        _calendarService = calendarService;
        Days = new();
        UpdateCurrentWeekRange();
    }

    public override async Task Enter()
    {
        _windowStartDate = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.DayOfWeek);
        await LoadUserAnimeList();
        await LoadScheduleAsync();
    }
        
    private async Task LoadUserAnimeList()
    {
        _watchingList.Clear();
        
        if(!MalService.IS_LOGGED_IN) return;
        
        List<MalAnimeData> watching = await _malService.GetUserAnimeList(AnimeStatusApi.watching);
        List<MalAnimeData> planToWatch = await _malService.GetUserAnimeList(AnimeStatusApi.plan_to_watch);

        foreach (MalAnimeData anime in watching)
        {
            if(anime.Node.Title != null) _watchingList.Add(anime.Node.Title);
        }
        foreach (MalAnimeData anime in planToWatch)
        {
            if(anime.Node.Title != null) _watchingList.Add(anime.Node.Title);
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
            var schedule = await _calendarService.GetScheduleAsync(_watchingList, daysToFetch.First(), daysToFetch.Last().AddDays(1));
            foreach (DaySchedule day in schedule)
            {
                _cachedDays[day.Date] = day;
            }
        }

        await ShowWindowAsync();
                
        IsLoading = false;
    }

    public void GoToClickedAnime(AnimeScheduleItem anime)
    {
        MainViewModel vm = App.ServiceProvider.GetRequiredService<MainViewModel>();
        if (anime.MalId.HasValue && anime.MalId.Value > 0)
        {
            vm.GoToAnime(anime.MalId.Value);
        }
        else
        {
            vm.GoToAnime(anime.Title);
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
    private async Task GoToToday()
    {
        _windowStartDate = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.DayOfWeek);
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
                var filteredItems = cachedDaySchedule.Items
                    .Where(item => !ShowOnlyMyAnime || _watchingList.Contains(item.Title))
                    .Select(item => EnhanceAnimeItem(item, currentDate));

                var displayDaySchedule = new DaySchedule
                {
                    Name = cachedDaySchedule.Name,
                    DayName = cachedDaySchedule.DayName,
                    Date = cachedDaySchedule.Date,
                    IsToday = cachedDaySchedule.IsToday,
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
        return new()
        {
            Title = original.Title,
            AiringAt = original.AiringAt,
            EpisodeInfo = original.EpisodeInfo ?? $"EP{original.Episode} • {original.Type}",
            Type = original.Type,
            Episode = original.Episode,
            IsBookmarked = _watchingList.Contains(original.Title),
            IsAiringNow = IsCurrentlyAiring(original.AiringAt, dayDate),
            ImageUrl = original.ImageUrl
        };
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