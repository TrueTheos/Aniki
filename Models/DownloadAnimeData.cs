using System.Collections.ObjectModel;
using Aniki.Services.Anime;
using Aniki.Services.Interfaces;

namespace Aniki.Models;

public partial class AnimeGroup : ObservableObject
{
    public string Title { get; }
    public ObservableCollection<DownloadedEpisode> Episodes { get; }
    public int MaxEpisodes { get; }
    [ObservableProperty] private int _watchedEpisodes = 0;
    public int MalId { get; }
    public string EpisodesProgressText => $"{WatchedEpisodes} / {MaxEpisodes} watched";

    public AnimeGroup(string title, ObservableCollection<DownloadedEpisode> episodes, int maxEp, int malId,
        IAnimeService animeService)
    {
        Title = title;
        Episodes = episodes;
        MaxEpisodes = maxEp;
        MalId = malId;

        Episodes.CollectionChanged += (_, _) => UpdateEpisodes();

        animeService.SubscribeToFieldChange(MalId, OnEpisodesCompletedCollectionChanged, AnimeField.MY_LIST_STATUS);
    }

    private void UpdateEpisodes()
    {
        foreach (DownloadedEpisode ep in Episodes)
        {
            ep.Watched = ep.EpisodeNumber <= WatchedEpisodes;
        }
    }
    
    private void OnEpisodesCompletedCollectionChanged(AnimeDetails e)
    {
        WatchedEpisodes = e.UserStatus?.EpisodesWatched ?? 0;
        UpdateEpisodes();

        OnPropertyChanged(nameof(EpisodesProgressText));
    }
}

public partial class DownloadedEpisode : ObservableObject
{
    public string FilePath { get; }
    public int EpisodeNumber { get; }
    public int? AbsoluteEpisodeNumber { get; }
    public int Season { get; }
    public string AnimeTitle { get; }
    public int Id { get;  }
    [ObservableProperty] 
    private bool _watched = false; 

    public DownloadedEpisode(string filePath, int episodeNumber, int? absoluteEpisodeNumber, string animeTitle, int id, int season)
    {
        FilePath = filePath;
        EpisodeNumber = episodeNumber;
        AbsoluteEpisodeNumber = absoluteEpisodeNumber;
        AnimeTitle = animeTitle;
        Id = id;
        Season = season;    
    }
}