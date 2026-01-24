using Aniki.Services.Cache;
using Avalonia.Media.Imaging;

namespace Aniki.Models;

public class UserData
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Picture { get; set; }
}

public class AnimeDetails : ObservableObject
{
    [CacheField(AnimeField.Id)] public int Id { get; set; }
    [CacheField(AnimeField.Title)] public string? Title { get; set; }
    [CacheField(AnimeField.MainPicture)] public AnimePicture? MainPicture { get; set; }
    [CacheField(AnimeField.Status)] public string? Status { get; set; }
    [CacheField(AnimeField.Synopsis)] public string? Synopsis { get; set; }
    [CacheField(AnimeField.AlterTitles)] public AlternativeTitles? AlternativeTitles { get; set; }
    
    private UserAnimeStatus? _userStatus;

    [CacheField(AnimeField.MyListStatus, true)]
    public UserAnimeStatus? UserStatus
    {
        get => _userStatus;
        set
        {
            if (_userStatus != value)
            {
                _userStatus = value;
                OnPropertyChanged();
            }
        }
    }
    
    [CacheField(AnimeField.Episodes)] public int? NumEpisodes { get; set; }
    [CacheField(AnimeField.Popularity)] public int? Popularity { get; set; }
    [CacheField(AnimeField.Picture)] public Bitmap? Picture { get; set; }
    [CacheField(AnimeField.Studios)] public string[]? Studios { get; set; }
    [CacheField(AnimeField.StartDate)] public string? StartDate { get; set; }
    [CacheField(AnimeField.Mean)] public float Mean { get; set; }
    [CacheField(AnimeField.Genres)] public string[]? Genres { get; set; }
    [CacheField(AnimeField.RelatedAnime)] public RelatedAnime[]? RelatedAnime { get; set; }
    [CacheField(AnimeField.TrailerUrl)] public string? TrailerUrl { get; set; }
    [CacheField(AnimeField.NumFav)] public int? NumFavorites { get; set; }
    [CacheField(AnimeField.Stats)] public AnimeStatistics? Statistics { get; set; }
    [CacheField(AnimeField.Videos)] public AnimeVideo[]? Videos { get; set; }
    [CacheField(AnimeField.MediaType)] public MediaType MediaType { get; set; }
    
    public AnimeDetails(){}
    
    public AnimeDetails(int id, string? title, AnimePicture? mainPicture, string? status, string? synopsis, AlternativeTitles? alternativeTitles,
        UserAnimeStatus? userStatus, int? numEpisodes, int? popularity, Bitmap? picture, string[]? studios, string? startDate,
        float mean, string[]? genres, string? trailerUrl, int? numFavorites, AnimeVideo[]? videos, RelatedAnime[] relatedAnime, AnimeStatistics statistics, MediaType mediaType)
    {
        Id = id;
        Title = title;
        MainPicture = mainPicture;
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
        MediaType = mediaType;
    }
    
    public AnimeCardData ToCardData()
    {
        return new AnimeCardData()
        {
            AnimeId = Id,
            Title = Title,
            ImageUrl = MainPicture == null ? null : string.IsNullOrEmpty(MainPicture.Large) ? MainPicture.Medium : MainPicture.Large,
            Score = Mean,
            UserStatus = UserStatus?.Status ?? AnimeStatus.None
        };
    }
}

public class AnimePicture
{
    public required string Medium { get; set; }
    public required string Large { get; set; }
}

public class AlternativeTitles
{
    public string[]? Synonyms { get; set; }
    public string? English { get; set; }
    public string? Japanese { get; set; }
}

public class UserAnimeStatus
{
    public enum UserAnimeStatusField
    {
        Status,
        Score,
        EpisodesWatched
    };
    public AnimeStatus Status { get; set; }
    public int Score { get; set; }
    public int EpisodesWatched { get; set; }
}

public class RelatedAnime
{
    public enum RelationType
    {
        Prequel,
        Sequel,
        Other
    };
    public AnimeDetails? Details { get; set; }
    public required RelationType Relation { get; set; }
}

public class AnimeStatistics
{
    public int NumListUsers { get; set; }
    public StatusStatistics? StatusStats { get; set; }
}

public class StatusStatistics
{
    public int Watching { get; set; }
    public int Completed { get; set; }
    public int OnHold { get; set; }
    public int Dropped { get; set; }
    public int PlanToWatch { get; set; }
}

public class RankingEntry
{
    public required AnimeDetails Details { get; set; }
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

public enum MediaType
{
    Unknown,
    TV,
    PV,
    TV_Short,
    TV_Special,
    OVA,
    ONA,
    Movie,
    Special,
    Music,
    Manga,
    Novel,
    One_Shot
}

public class AnimeVideo
{
    public required string Title { get; set; }
    public required string Url { get; set; }
    public required string Thumbnail { get; set; }
}