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
        private List<DaySchedule> _allDays = [];
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

        public void GoToClickedAnime(AnimeScheduleItem anime)
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
            await ShowWindowAsync();
        }
        
        [RelayCommand]
        public async Task GoBackWeek()
        {
            if (_allDays.Any())
            {
                _windowStartDate = _windowStartDate.AddDays(-7);
                await ShowWindowAsync();
            }
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
        private async Task GoForwardWeek()
        {
            if (_allDays.Any())
            {
                _windowStartDate = _windowStartDate.AddDays(7);
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
            List<DaySchedule> newDays = new List<DaySchedule>();

            for (int i = 0; i < 7; i++)
            {
                DateTime currentDate = _windowStartDate.AddDays(i);
                string dayName = currentDate.DayOfWeek.ToString();

                DaySchedule? existingDay = _allDays.FirstOrDefault(d =>
                    string.Equals(d.Name, dayName, StringComparison.OrdinalIgnoreCase));

                if (existingDay != null)
                {
                    DaySchedule daySchedule = new DaySchedule
                    {
                        Name = dayName,
                        DayName = currentDate.ToString("dddd"),
                        Date = currentDate,
                        IsToday = currentDate.Date == DateTime.Today,
                        Items = new(
                            existingDay.Items.Select(item => EnhanceAnimeItem(item, currentDate)))
                    };
                    newDays.Add(daySchedule);
                }
                else
                {
                    newDays.Add(new()
                    {
                        Name = dayName,
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
            _updateTimer.Elapsed += (s, e) =>
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
