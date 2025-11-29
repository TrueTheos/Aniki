using Avalonia.Media.Imaging;

namespace Aniki.Models;

public class UserData
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Picture { get; set; }
}

public class AnimeData
{
    public required AnimeDetails Details { get; set; }
    public UserAnimeStatus? UserStatus { get; set; }
}

public class AnimeDetails : ObservableObject
{
    [CacheField(AnimeField.ID)] public int Id { get; set; }
    [CacheField(AnimeField.TITLE)] public string? Title { get; set; }
    [CacheField(AnimeField.MAIN_PICTURE)] public AnimePicture? MainPicture { get; set; }
    [CacheField(AnimeField.STATUS)] public string? Status { get; set; }
    [CacheField(AnimeField.SYNOPSIS)] public string? Synopsis { get; set; }
    [CacheField(AnimeField.ALTER_TITLES)] public AlternativeTitles? AlternativeTitles { get; set; }
    
    private UserAnimeStatus? _userStatus;

    [CacheField(AnimeField.MY_LIST_STATUS, true)]
    public UserAnimeStatus? UserStatus
    {
        get => _userStatus;
        set
        {
            if (_userStatus != value)
            {
                _userStatus = value;
                OnPropertyChanged(nameof(UserStatus));
            }
        }
    }
    
    [CacheField(AnimeField.EPISODES)] public int? NumEpisodes { get; set; }
    [CacheField(AnimeField.POPULARITY)] public int? Popularity { get; set; }
    [CacheField(AnimeField.PICTURE)] public Bitmap? Picture { get; set; }
    [CacheField(AnimeField.STUDIOS)] public Studio[]? Studios { get; set; }
    [CacheField(AnimeField.START_DATE)] public string? StartDate { get; set; }
    [CacheField(AnimeField.MEAN)] public float Mean { get; set; }
    [CacheField(AnimeField.GENRES)] public Genre[]? Genres { get; set; }
    [CacheField(AnimeField.RELATED_ANIME)] public RelatedAnime[]? RelatedAnime { get; set; }
    [CacheField(AnimeField.TRAILER_URL)] public string? TrailerUrl { get; set; }
    [CacheField(AnimeField.NUM_FAV)] public int? NumFavorites { get; set; }
    [CacheField(AnimeField.STATS)] public AnimeStatistics? Statistics { get; set; }
    [CacheField(AnimeField.VIDEOS)] public AnimeVideo[]? Videos { get; set; }
    
    public AnimeDetails(int id, string? title, AnimePicture? mainPicture, string? status, string? synopsis, AlternativeTitles? alternativeTitles,
        UserAnimeStatus? userStatus, int? numEpisodes, int? popularity, Bitmap? picture, Studio[]? studios, string? startDate,
        float mean, Genre[]? genres, string? trailerUrl, int? numFavorites, AnimeVideo[]? videos, RelatedAnime[] relatedAnime, AnimeStatistics statistics)
    {
        Id = id;
        Title = title;
        MainPicture = mainPicture;
        Status = status;
        Synopsis = synopsis;
        AlternativeTitles = alternativeTitles;
        UserStatus = userStatus;
        NumEpisodes = numEpisodes;
        Popularity = popularity;
        Picture = picture;
        Studios = studios;
        StartDate = startDate;
        Mean = mean;
        Genres = genres;
        RelatedAnime = relatedAnime;
        TrailerUrl = trailerUrl;
        NumFavorites = numFavorites;
        Statistics = statistics;
        Videos = videos;
    }
    
    public AnimeCardData ToCardData()
    {
        return new AnimeCardData()
        {
            AnimeId = Id,
            Title = Title,
            ImageUrl = MainPicture == null ? null : string.IsNullOrEmpty(MainPicture.Large) ? MainPicture.Medium : MainPicture.Large,
            Score = Mean,
            MyListStatus = UserStatus?.Status ?? AnimeStatus.None
        };
    }
}

public class AnimePicture
{
    public required string Medium { get; set; }
    public required string Large { get; set; }
}

public class Studio
{
    public int Id { get; set; }
    public required string Name { get; set; }
}

public class Genre
{
    public int Id { get; set; }
    public required string Name { get; set; }
}

public class AlternativeTitles
{
    public string[]? Synonyms { get; set; }
    public string? English { get; set; }
    public string? Japanese { get; set; }
}

public class UserAnimeStatus
{
    public AnimeStatus Status { get; set; }
    public int Score { get; set; }
    public int EpisodesWatched { get; set; }
}

public class RelatedAnime
{
    public AnimeDetails? Details { get; set; }
    public required string RelationType { get; set; }
}

public class AnimeStatistics
{
    public int NumListUsers { get; set; }
    public StatusStatistics? StatusStats { get; set; }
}

public class StatusStatistics
{
    public string? Watching { get; set; }
    public string? Completed { get; set; }
    public string? OnHold { get; set; }
    public string? Dropped { get; set; }
    public string? PlanToWatch { get; set; }
}

public class AnimeSearchResult
{
    public required AnimeDetails Details { get; set; }
    public int SearchScore { get; set; }
}

public class RankingEntry
{
    public required AnimeDetails Details { get; set; }
    public int Rank { get; set; }
}

public enum AnimeStatus
{
    None,
    Watching,
    Completed,
    OnHold,
    Dropped,
    PlanToWatch
}

public enum RankingCategory
{
    Airing,
    Upcoming,
    AllTime,
    ByPopularity
}


public class AnimeVideo
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Url { get; set; }
    public required string Thumbnail { get; set; }
}