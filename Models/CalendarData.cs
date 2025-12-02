using System.Collections.ObjectModel;
using Aniki.Services.Anime;
using Aniki.Services.Auth;
using Tmds.DBus.Protocol;

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
    public Dictionary<ILoginProvider.ProviderType, int> ProviderId { get; set; } = new();

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private DateTime _airingAt;
    
    [ObservableProperty]
    private string _imageUrl = "";

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
    private float _mean;

    public string EpisodeText => Episode > 0 ? $"EP{Episode}" : "";

    public int? GetId()
    {
        if (!ProviderId.TryGetValue(AnimeService.CurrentProviderType, out int id)) return null;
        return id;
    }

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
}