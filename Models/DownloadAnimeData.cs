using System.Collections.ObjectModel;
using Aniki.Services.Anime;

namespace Aniki.Models;

internal sealed partial class AnimeGroup : ObservableObject
{
    public string Title { get; }
    public string? ThumbnailUrl { get; }
    public ObservableCollection<DownloadedEpisode> Episodes { get; }
    public int MaxEpisodes { get; }
    [ObservableProperty] public partial int WatchedEpisodes { get; set; }
    public int MalId { get; }

    public string EpisodesProgressText => $"{WatchedEpisodes} / {(MaxEpisodes == 0 ? "?" : $"{MaxEpisodes}")} watched";

    public int OnDiskCount => Episodes.Count;
    public bool HasOnDisk => Episodes.Count > 0;
    public int UnwatchedOnDiskCount => Episodes.Count(ep => !ep.Watched);
    public bool HasUnwatchedOnDisk => UnwatchedOnDiskCount > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDownloadNext))]
    [NotifyPropertyChangedFor(nameof(UnseenReleasedCount))]
    [NotifyPropertyChangedFor(nameof(HasUnseenReleased))]
    public partial int ReleasedEpisodes { get; set; }

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FirstDisplayedEpisode))]
    [NotifyPropertyChangedFor(nameof(AdditionalDisplayedEpisodes))]
    [NotifyPropertyChangedFor(nameof(IsCollapsed))]
    private bool _isExpanded;

    public bool HasMoreEpisodes => Episodes.Count > 1;
    public bool IsCollapsed => !IsExpanded;

    public DownloadedEpisode? FirstDisplayedEpisode
    {
        get
        {
            if (!HasOnDisk)
                return null;

            if (IsExpanded)
                return Episodes.FirstOrDefault();

            return PreviewEpisode;
        }
    }

    public IEnumerable<DownloadedEpisode> AdditionalDisplayedEpisodes =>
        IsExpanded && Episodes.Count > 1 ? Episodes.Skip(1) : [];

    public DownloadedEpisode? PreviewEpisode
    {
        get
        {
            if (Episodes.Count == 0)
                return null;

            int nextEpisode = WatchedEpisodes + 1;
            DownloadedEpisode? exactNext = Episodes
                .Where(ep => ep.EpisodeNumber == nextEpisode)
                .OrderBy(ep => ep.Season)
                .ThenBy(ep => ep.EpisodeNumber)
                .FirstOrDefault();

            if (exactNext != null)
                return exactNext;

            DownloadedEpisode? firstUnwatched = Episodes
                .Where(ep => !ep.Watched)
                .OrderBy(ep => ep.Season)
                .ThenBy(ep => ep.EpisodeNumber)
                .FirstOrDefault();

            if (firstUnwatched != null)
                return firstUnwatched;

            return Episodes
                .OrderByDescending(ep => ep.Season)
                .ThenByDescending(ep => ep.EpisodeNumber)
                .First();
        }
    }

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
        OnPropertyChanged(nameof(HasMoreEpisodes));
        OnPropertyChanged(nameof(IsCollapsed));
        OnPropertyChanged(nameof(FirstDisplayedEpisode));
        OnPropertyChanged(nameof(AdditionalDisplayedEpisodes));
        OnPropertyChanged(nameof(PreviewEpisode));
    }

    [RelayCommand]
    private void ToggleExpanded()
    {
        if (!HasMoreEpisodes)
            return;

        IsExpanded = !IsExpanded;
    }

    private void OnEpisodesCompletedCollectionChanged(AnimeDetails e)
    {
        WatchedEpisodes = e.UserStatus?.EpisodesWatched ?? 0;
        UpdateEpisodes();
        NotifyComputedProperties();
        OnPropertyChanged(nameof(EpisodesProgressText));
    }
}

internal sealed partial class DownloadedEpisode : ObservableObject
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
