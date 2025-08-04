using Aniki.Models;
using Aniki.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Aniki.ViewModels
{
    public partial class CalendarViewModel : ViewModelBase
    {
        private Dictionary<DateTime, DaySchedule> _cachedDays = new();
        private DateTime _windowStartDate;

        [ObservableProperty]
        private ObservableCollection<DaySchedule> _days;

        private readonly List<string> _watchingList = new() { };

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _currentWeekRange = "";

        private MainViewModel _mainViewModel;

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

        public CalendarViewModel(MainViewModel mainVm)
        {
            _mainViewModel = mainVm;
            Days = new();
            UpdateCurrentWeekRange();
        }

        public override async Task Enter()
        {
            _windowStartDate = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.DayOfWeek);
            await LoadScheduleAsync();
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
                var schedule = await CalendarService.GetScheduleAsync(_watchingList, daysToFetch.First(), daysToFetch.Last().AddDays(1));
                foreach (DaySchedule day in schedule)
                {
                    _cachedDays[day.Date] = day;
                }
            }

            await ShowWindowAsync();
                
            IsLoading = false;
        }

        public void GoToClickedAnime(AnimeScheduleItem? anime)
        {
            if (anime.MalId.HasValue && anime.MalId.Value > 0)
            {
                _mainViewModel.GoToAnime(anime.MalId.Value);
            }
            else
            {
                _mainViewModel.GoToAnime(anime.Title);
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

        private async Task ShowWindowAsync()
        {
            ShowWindow();
            UpdateCurrentWeekRange();

            OnPropertyChanged(nameof(CurrentTimeOffset));
        }

        private void ShowWindow()
        {
            List<DaySchedule> newDays = new List<DaySchedule>();

            for (int i = 0; i < 7; i++)
            {
                DateTime currentDate = _windowStartDate.AddDays(i);
                if (_cachedDays.TryGetValue(currentDate.Date, out DaySchedule? cachedDaySchedule))
                {
                    cachedDaySchedule.Items = new ObservableCollection<AnimeScheduleItem>(
                        cachedDaySchedule.Items.Select(item => EnhanceAnimeItem(item, currentDate)));
                    newDays.Add(cachedDaySchedule);
                }
                else
                {
                    newDays.Add(new()
                    {
                        Name = currentDate.DayOfWeek.ToString(),
                        DayName = currentDate.ToString("dddd"),
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
                ImageUrl = original.ImageUrl,
                AiringAt = original.AiringAt,
                EpisodeInfo = original.EpisodeInfo ?? $"EP{original.Episode} • {original.Type}",
                Type = original.Type,
                Episode = original.Episode,
                IsBookmarked = _watchingList.Contains(original.Title),
                IsAiringNow = IsCurrentlyAiring(original.AiringAt, dayDate)
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

        private System.Timers.Timer _updateTimer;

        public void StartLiveUpdates()
        {
            _updateTimer = new(60000);
            _updateTimer.Elapsed += (_, _) =>
            {
                OnPropertyChanged(nameof(CurrentTimeOffset));

                foreach (DaySchedule day in Days.Where(d => d.IsToday))
                {
                    foreach (AnimeScheduleItem item in day.Items)
                    {
                        bool wasAiring = item.IsAiringNow;
                        item.IsAiringNow = IsCurrentlyAiring(item.AiringAt, day.Date);

                        if (wasAiring != item.IsAiringNow)
                        {
                            //todo
                        }
                    }
                }
            };
            _updateTimer.Start();
        }

        public void StopLiveUpdates()
        {
            _updateTimer?.Stop();
            _updateTimer?.Dispose();
        }
    }
}
