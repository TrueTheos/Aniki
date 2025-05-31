using Aniki.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aniki.Models
{
    public partial class DaySchedule : ObservableObject
    {
        [ObservableProperty]
        private string _name = "";

        [ObservableProperty]
        private string _dayName = "";

        [ObservableProperty]
        private DateTime _date;

        [ObservableProperty]
        private bool _isToday;

        [ObservableProperty]
        private ObservableCollection<AnimeScheduleItem> _items = new();
    }

    public partial class AnimeScheduleItem : ObservableObject
    {
        [ObservableProperty]
        private string _title = "";

        [ObservableProperty]
        private string _imageUrl = "";

        [ObservableProperty]
        private DateTime _airingAt;

        [ObservableProperty]
        private string _episodeInfo = "";

        [ObservableProperty]
        private string _type = "TV";

        [ObservableProperty]
        private int _episode;

        [ObservableProperty]
        private bool _isBookmarked;

        [ObservableProperty]
        private bool _isAiringNow;

        [ObservableProperty]
        private string _status = "";

        [ObservableProperty]
        private double _rating;

        [ObservableProperty]
        private string _genre = "";

        [ObservableProperty]
        private string _studio = "";

        [ObservableProperty]
        private int _duration = 24; // Duration in minutes

        [ObservableProperty]
        private string _description = "";

        // Computed properties
        public string AirTimeFormatted => AiringAt.ToString("h:mm tt");

        public string EpisodeText => Episode > 0 ? $"EP{Episode}" : "";

        public string TypeAndEpisode => !string.IsNullOrEmpty(EpisodeText)
            ? $"{EpisodeText} • {Type}"
            : Type;

        public TimeSpan TimeUntilAiring
        {
            get
            {
                var now = DateTime.Now;
                var airTime = AiringAt;

                // If it's today and hasn't aired yet
                if (airTime.Date == now.Date && airTime > now)
                {
                    return airTime - now;
                }

                // If it's a future date
                if (airTime.Date > now.Date)
                {
                    return airTime - now;
                }

                // Already aired
                return TimeSpan.Zero;
            }
        }

        public string CountdownText
        {
            get
            {
                var timeUntil = TimeUntilAiring;

                if (timeUntil <= TimeSpan.Zero)
                {
                    return IsAiringNow ? "Airing Now" : "Aired";
                }

                if (timeUntil.TotalDays >= 1)
                {
                    return $"{(int)timeUntil.TotalDays}d {timeUntil.Hours}h";
                }

                if (timeUntil.TotalHours >= 1)
                {
                    return $"{(int)timeUntil.TotalHours}h {timeUntil.Minutes}m";
                }

                return $"{timeUntil.Minutes}m";
            }
        }
    }

    // Enum for anime types with colors
    public enum AnimeType
    {
        TV,
        Movie,
        OVA,
        ONA,
        Special,
        Music
    }

    // Additional helper classes
    public class AnimeGenre
    {
        public string Name { get; set; } = "";
        public string Color { get; set; } = "#666666";
    }

    public class AnimeStudio
    {
        public string Name { get; set; } = "";
        public string Logo { get; set; } = "";
    }

    // For grouping and filtering
    public class CalendarFilter
    {
        public List<string> Genres { get; set; } = new();
        public List<string> Studios { get; set; } = new();
        public List<AnimeType> Types { get; set; } = new();
        public bool OnlyWatching { get; set; }
        public bool OnlyAiring { get; set; }
        public TimeSpan? TimeRange { get; set; }
    }
}
