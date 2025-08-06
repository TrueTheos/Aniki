using System.Collections.ObjectModel;

namespace Aniki.Models;

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
    public int? MalId { get; set; }

    [ObservableProperty]
    private string title = "";

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

    public string EpisodeText => Episode > 0 ? $"EP{Episode}" : "";

    public TimeSpan TimeUntilAiring
    {
        get
        {
            DateTime now = DateTime.Now;
            DateTime airTime = AiringAt;

            if (airTime.Date == now.Date && airTime > now)
            {
                return airTime - now;
            }

            if (airTime.Date > now.Date)
            {
                return airTime - now;
            }

            return TimeSpan.Zero;
        }
    }

    public string CountdownText
    {
        get
        {
            TimeSpan timeUntil = TimeUntilAiring;

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

public enum AnimeType
{
    TV,
    Movie,
    OVA,
    ONA,
    Special,
    Music
}

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

public class CalendarFilter
{
    public List<string> Genres { get; set; } = new();
    public List<string> Studios { get; set; } = new();
    public List<AnimeType> Types { get; set; } = new();
    public bool OnlyWatching { get; set; }
    public bool OnlyAiring { get; set; }
    public TimeSpan? TimeRange { get; set; }
}