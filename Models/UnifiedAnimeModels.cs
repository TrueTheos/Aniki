using Aniki.Services.Cache;
using Avalonia.Media.Imaging;

namespace Aniki.Models;

internal sealed class UserData
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Picture { get; set; }
}

internal enum AnimeField 
{
    Id,
    Title, 
    MainPicture, 
    Status, 
    Synopsis, 
    AlterTitles, 
    MyListStatus, 
    Episodes,
    Popularity, 
    Picture, 
    Studios, 
    StartDate, 
    Mean, 
    Genres, 
    RelatedAnime, 
    Videos, 
    NumFav, 
    Stats, 
    TrailerUrl,
    MediaType
}

internal sealed class AnimeDetails : ObservableObject
{
    [CacheField(AnimeField.Id)] public int Id { get; set; }
    [CacheField(AnimeField.Title)] public string? Title { get; set; }
    [CacheField(AnimeField.MainPicture)] public AnimePicture? MainPicture { get; set; }
    [CacheField(AnimeField.Status)] public string? Status { get; set; }
    [CacheField(AnimeField.Synopsis)] public string? Synopsis { get; set; }
    [CacheField(AnimeField.AlterTitles)] public AlternativeTitles? AlternativeTitles { get; set; }

    [CacheField(AnimeField.MyListStatus, true)]
    public UserAnimeStatus? UserStatus
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
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

internal sealed class AnimePicture
{
    public required string Medium { get; set; }
    public required string Large { get; set; }
}

internal sealed class AlternativeTitles
{
    public string[]? Synonyms { get; set; }
    public string? English { get; set; }
    public string? Japanese { get; set; }
}

internal sealed class UserAnimeStatus
{
    internal enum UserAnimeStatusField
    {
        Status,
        Score,
        EpisodesWatched
    };
    public AnimeStatus Status { get; set; }
    public int Score { get; set; }
    public int EpisodesWatched { get; set; }
}

internal sealed class RelatedAnime
{
    internal enum RelationType
    {
        Prequel,
        Sequel,
        Summary,
        FullStory,
        Other
    };
    public AnimeDetails? Details { get; set; }
    public required RelationType Relation { get; set; }
}

internal sealed class AnimeStatistics
{
    public int NumListUsers { get; set; }
    public StatusStatistics? StatusStats { get; set; }
}

internal sealed class StatusStatistics
{
    public int Watching { get; set; }
    public int Completed { get; set; }
    public int OnHold { get; set; }
    public int Dropped { get; set; }
    public int PlanToWatch { get; set; }
}

internal sealed class RankingEntry
{
    public required AnimeDetails Details { get; set; }
}

internal enum AnimeStatus
{
    None,
    Watching,
    Completed,
    OnHold,
    Dropped,
    PlanToWatch
}

internal enum RankingCategory
{
    Airing,
    Upcoming,
    AllTime,
    ByPopularity
}

internal enum MediaType
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
    One_Shot,
    CM
}

internal sealed class AnimeVideo
{
    public required string Title { get; set; }
    public required string Url { get; set; }
    public string? Thumbnail { get; set; }
}