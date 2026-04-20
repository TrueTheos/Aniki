using System.Collections.ObjectModel;
using System.Linq;
using Aniki.Services.Anime;

namespace Aniki.Models;

public partial class AnimeGroup : ObservableObject
{
    public string Title { get; }
    public string? ThumbnailUrl { get; }
    public ObservableCollection<DownloadedEpisode> Episodes { get; }
    public int MaxEpisodes { get; }
    [ObservableProperty] private int _watchedEpisodes;
    public int MalId { get; }

    public string EpisodesProgressText => $"{WatchedEpisodes} / {MaxEpisodes} watched";

    public int OnDiskCount => Episodes.Count;
    public bool HasOnDisk => Episodes.Count > 0;
    public int UnwatchedOnDiskCount => Episodes.Count(ep => !ep.Watched);
    public bool HasUnwatchedOnDisk => UnwatchedOnDiskCount > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDownloadNext))]
    [NotifyPropertyChangedFor(nameof(UnseenReleasedCount))]
    [NotifyPropertyChangedFor(nameof(HasUnseenReleased))]
    private int _releasedEpisodes;

    public int UnseenReleasedCount => Math.Max(0, ReleasedEpisodes - WatchedEpisodes);
    public bool HasUnseenReleased => UnseenReleasedCount > 0;

    public int? NextEpisodeToDownload
    {
        get
        {
            int highestOnDisk = Episodes.Any() ? Episodes.Max(ep => ep.EpisodeNumber) : 0;
            int nextEp = Math.Max(WatchedEpisodes, highestOnDisk) + 1;
            if (MaxEpisodes > 0 && nextEp > MaxEpisodes) return null;
            return nextEp;
        }
    }

    public bool CanDownloadNext =>
        NextEpisodeToDownload.HasValue &&
        ReleasedEpisodes > 0 &&
        NextEpisodeToDownload.Value <= ReleasedEpisodes;

    public string NextEpisodeToDownloadText => NextEpisodeToDownload.HasValue ? $"Download Ep {NextEpisodeToDownload}" : "";

    public AnimeGroup(string title, string? thumbnailUrl, ObservableCollection<DownloadedEpisode> episodes,
        int maxEp, int watchedEp, int malId, IAnimeService animeService)
    {
        Title = title;
        ThumbnailUrl = thumbnailUrl;
        Episodes = episodes;
        Episodes.CollectionChanged += (_, _) => OnEpisodesCollectionChanged();
        MaxEpisodes = maxEp;
        WatchedEpisodes = watchedEp;
        MalId = malId;

        animeService.SubscribeToFieldChange(MalId, OnEpisodesCompletedCollectionChanged, AnimeField.MyListStatus);
        UpdateEpisodes();
    }

    private void OnEpisodesCollectionChanged()
    {
        UpdateEpisodes();
        NotifyComputedProperties();
    }

    private void UpdateEpisodes()
    {
        foreach (DownloadedEpisode ep in Episodes)
            ep.Watched = ep.EpisodeNumber <= WatchedEpisodes;
    }

    private void NotifyComputedProperties()
    {
        OnPropertyChanged(nameof(OnDiskCount));
        OnPropertyChanged(nameof(HasOnDisk));
        OnPropertyChanged(nameof(UnwatchedOnDiskCount));
        OnPropertyChanged(nameof(HasUnwatchedOnDisk));
        OnPropertyChanged(nameof(NextEpisodeToDownload));
        OnPropertyChanged(nameof(CanDownloadNext));
        OnPropertyChanged(nameof(NextEpisodeToDownloadText));
        OnPropertyChanged(nameof(UnseenReleasedCount));
        OnPropertyChanged(nameof(HasUnseenReleased));
    }

    private void OnEpisodesCompletedCollectionChanged(AnimeDetails e)
    {
        WatchedEpisodes = e.UserStatus?.EpisodesWatched ?? 0;
        UpdateEpisodes();
        NotifyComputedProperties();
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
    public int Id { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUnwatched))]
    private bool _watched;

    public bool IsUnwatched => !Watched;

    public DownloadedEpisode(string filePath, int episodeNumber, int? absoluteEpisodeNumber,
        string animeTitle, int id, int season)
    {
        FilePath = filePath;
        EpisodeNumber = episodeNumber;
        AbsoluteEpisodeNumber = absoluteEpisodeNumber;
        AnimeTitle = animeTitle;
        Id = id;
        Season = season;
    }
}
