using Aniki.Models;
using Aniki.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aniki.ViewModels
{
    public partial class CalendarViewModel : ViewModelBase
    {
        private List<DaySchedule> _allDays = new();
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
                var now = DateTime.Now;
                var minutesFromMidnight = now.Hour * 60 + now.Minute;
                const double pxPerMinute = 1440.0 / (24 * 60);
                return minutesFromMidnight * pxPerMinute;
            }
        }

        public CalendarViewModel(MainViewModel mainVM)
        {
            _mainViewModel = mainVM;
            Days = new ObservableCollection<DaySchedule>();
            UpdateCurrentWeekRange();
        }

        public override Task Enter()
        {
            _ = LoadScheduleAsync();
            return Task.CompletedTask;
        }

        private async Task LoadScheduleAsync()
        {
            try
            {
                IsLoading = true;
                _allDays = await CalendarService.GetWeeklyScheduleAsync(_watchingList);
                _windowStartDate = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.DayOfWeek);
                await ShowWindowAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading schedule: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public async Task GoToStart()
        {
            if (_allDays.Any())
            {
                _windowStartDate = GetEarliestDate();
                await ShowWindowAsync();
            }
        }

        public void GoToClickedAnime(AnimeScheduleItem anime)
        {
            _mainViewModel.GoToAnime(anime.Title);
        }

        [RelayCommand]
        private async Task GoBackDay()
        {
            _windowStartDate = _windowStartDate.AddDays(-1);
            await ShowWindowAsync();
        }

        [RelayCommand]
        private async Task GoToToday()
        {
            _windowStartDate = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.DayOfWeek);
            await ShowWindowAsync();
        }

        [RelayCommand]
        private async Task GoForwardDay()
        {
            _windowStartDate = _windowStartDate.AddDays(1);
            await ShowWindowAsync();
        }

        [RelayCommand]
        private async Task GoToEnd()
        {
            if (_allDays.Any())
            {
                var latestDate = GetLatestDate();
                _windowStartDate = latestDate.AddDays(-6);
                await ShowWindowAsync();
            }
        }

        private async Task ShowWindowAsync()
        {
            ShowWindow();
            UpdateCurrentWeekRange();

            OnPropertyChanged(nameof(CurrentTimeOffset));
        }

        private void ShowWindow()
        {
            var newDays = new List<DaySchedule>();

            for (int i = 0; i < 7; i++)
            {
                var currentDate = _windowStartDate.AddDays(i);
                var dayName = currentDate.DayOfWeek.ToString();

                var existingDay = _allDays.FirstOrDefault(d =>
                    string.Equals(d.Name, dayName, StringComparison.OrdinalIgnoreCase));

                if (existingDay != null)
                {
                    var daySchedule = new DaySchedule
                    {
                        Name = dayName,
                        DayName = currentDate.ToString("dddd"),
                        Date = currentDate,
                        IsToday = currentDate.Date == DateTime.Today,
                        Items = new ObservableCollection<AnimeScheduleItem>(
                            existingDay.Items.Select(item => EnhanceAnimeItem(item, currentDate)))
                    };
                    newDays.Add(daySchedule);
                }
                else
                {
                    newDays.Add(new DaySchedule
                    {
                        Name = dayName,
                        DayName = currentDate.ToString("dddd"),
                        Date = currentDate,
                        IsToday = currentDate.Date == DateTime.Today,
                        Items = new ObservableCollection<AnimeScheduleItem>()
                    });
                }
            }

            Days.Clear();
            foreach (var day in newDays)
            {
                Days.Add(day);
            }
        }

        private AnimeScheduleItem EnhanceAnimeItem(AnimeScheduleItem original, DateTime dayDate)
        {
            return new AnimeScheduleItem
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

            var now = DateTime.Now;
            var airingDateTime = dayDate.Date.Add(airingTime.TimeOfDay);

            return Math.Abs((now - airingDateTime).TotalMinutes) <= 30;
        }

        private DateTime GetEarliestDate()
        {
            return _allDays
                .Select(d => ParseDayName(d.Name))
                .DefaultIfEmpty(DateTime.Today)
                .Min();
        }

        private DateTime GetLatestDate()
        {
            return _allDays
                .Select(d => ParseDayName(d.Name))
                .DefaultIfEmpty(DateTime.Today)
                .Max();
        }

        private DateTime ParseDayName(string dayName)
        {
            if (Enum.TryParse<DayOfWeek>(dayName, true, out var dayOfWeek))
            {
                var today = DateTime.Today;
                var daysUntilTarget = ((int)dayOfWeek - (int)today.DayOfWeek + 7) % 7;
                return today.AddDays(daysUntilTarget);
            }
            return DateTime.Today;
        }

        private void UpdateCurrentWeekRange()
        {
            var endDate = _windowStartDate.AddDays(6);
            CurrentWeekRange = $"{_windowStartDate:MMM d} - {endDate:MMM d, yyyy}";
        }

        private System.Timers.Timer _updateTimer;

        public void StartLiveUpdates()
        {
            _updateTimer = new System.Timers.Timer(60000);
            _updateTimer.Elapsed += (s, e) =>
            {
                OnPropertyChanged(nameof(CurrentTimeOffset));

                foreach (var day in Days.Where(d => d.IsToday))
                {
                    foreach (var item in day.Items)
                    {
                        var wasAiring = item.IsAiringNow;
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
