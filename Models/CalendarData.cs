using System.Collections.ObjectModel;
using Aniki.Services.Anime;
using Aniki.Services.Auth;

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
    private string _description = "";

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

    public AnimeScheduleItem(string title, string description, DateTime airingAt, int episode, string episodeInfo, string type, string imageUrl, float mean)
    {
        Title        = title;
        Description  = description;
        AiringAt     = airingAt;
        Episode      = episode;
        EpisodeInfo  = episodeInfo;
        Type         = type;
        ImageUrl     = imageUrl;
        Mean         = mean;
    }

    public int? GetId()
    {
        if (!ProviderId.TryGetValue(AnimeService.CurrentProviderType, out int id)) return null;
        return id;
    }
}