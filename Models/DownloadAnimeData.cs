using System.Collections.ObjectModel;

namespace Aniki.Models;

public class AnimeGroup
{
    public string Title { get; }
    public ObservableCollection<DownloadedEpisode> Episodes { get; }
    public int TotalEpisodes => Episodes.Count;
    
    public AnimeGroup(string title, ObservableCollection<DownloadedEpisode> episodes)
    {
        Title = title;
        Episodes = episodes;
    }
}

public class DownloadedEpisode
{
    public string FilePath { get; }
    public int EpisodeNumber { get; }
    public int? AbsoluteEpisodeNumber { get; }
    public int Season { get; }
    public string Title { get; }
    public int Id { get;  }

    public DownloadedEpisode(string filePath, int episodeNumber, int? absoluteEpisodeNumber, string title, int id, int season)
    {
        FilePath = filePath;
        EpisodeNumber = episodeNumber;
        AbsoluteEpisodeNumber = absoluteEpisodeNumber;
        Title = title;
        Id = id;
        Season = season;    
    }
}