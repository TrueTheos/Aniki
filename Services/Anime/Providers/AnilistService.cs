using System.Net.Http.Headers;
using Aniki.Models.Anilist;
using Aniki.Services.Anime;
using Aniki.Services.Auth;
using Aniki.Services.Interfaces;
using Avalonia.Media.Imaging;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;

namespace Aniki.Services;

public class AnilistService : IAnimeProvider
{
    private readonly GraphQLHttpClient _client;
    private readonly HttpClient _httpClient;
    private readonly ISaveService _saveService;
    private bool _isLoggedIn;
    private int? _currentUserId;

    public ILoginProvider.ProviderType Provider => ILoginProvider.ProviderType.AniList;
    public bool IsLoggedIn => _isLoggedIn;

    public AnilistService(ISaveService saveService)
    {
        _client = new GraphQLHttpClient("https://graphql.anilist.co", new SystemTextJsonSerializer());
        _httpClient = new HttpClient();
        _saveService = saveService;
    }

    public void Init(string? accessToken)
    {
        if (!string.IsNullOrEmpty(accessToken))
        {
            _client.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            _isLoggedIn = true;
            Task.Run(async () => { _currentUserId = (await GetUserDataAsync()).Id; });
        }
        else
        {
            _isLoggedIn = false;
            _currentUserId = null;
        }
    }

    public async Task<UserData> GetUserDataAsync()
    {
        if (!_isLoggedIn) return new UserData();

        var request = new GraphQLRequest
        {
            Query = @"
                query {
                    Viewer {
                        id
                        name
                        avatar {
                            large
                        }
                    }
                }"
        };

        var response = await _client.SendQueryAsync<ViewerResponse>(request);

        if (response.Data?.Viewer == null)
            throw new InvalidOperationException("Failed to get user data");

        return new UserData
        {
            Id = response.Data.Viewer.id,
            Name = response.Data.Viewer.name,
            Picture = response.Data.Viewer.avatar?.large
        };
    }

    public async Task<List<AnimeDetails>> GetUserAnimeListAsync(AnimeStatus status = AnimeStatus.None)
    {
        if (!_isLoggedIn) return new List<AnimeDetails>();

        var statusFilter = status != AnimeStatus.None ? ConvertToAnilistStatus(status) : null;
        
        var request = new GraphQLRequest
        {
            Query = @"
                query ($userId: Int, $status: MediaListStatus) {
                    MediaListCollection(userId: $userId, type: ANIME, status: $status) {
                        lists {
                            entries {
                                id
                                status
                                score(format: POINT_10)
                                progress
                                media {
                                    id
                                    title {
                                        romaji
                                        english
                                        native
                                    }
                                    coverImage {
                                        large
                                        medium
                                    }
                                    status
                                    description
                                    episodes
                                    meanScore
                                    popularity
                                    studios {
                                        nodes {
                                            id
                                            name
                                        }
                                    }
                                    startDate {
                                        year
                                        month
                                        day
                                    }
                                    genres
                                    favourites
                                    trailer {
                                        id
                                        site
                                    }
                                    relations {
                                        edges {
                                            relationType
                                            node {
                                                id
                                                title {
                                                    romaji
                                                    english
                                                }
                                                episodes
                                                status
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }",
            Variables = new
            {
                userId = (await GetUserDataAsync()).Id,
                status = statusFilter
            }
        };

        var response = await _client.SendQueryAsync<MediaListCollectionResponse>(request);
        var animeList = new List<AnimeDetails>();

        if (response.Data?.MediaListCollection?.lists != null)
        {
            foreach (var list in response.Data.MediaListCollection.lists)
            {
                if (list.entries != null)
                {
                    foreach (var entry in list.entries)
                    {
                        animeList.Add(await ConvertAnilistToUnified(entry.media));
                    }
                }
            }
        }

        return animeList;
    }

    public async Task RemoveFromUserListAsync(int animeId)
    {
        if (!_isLoggedIn) return;

        var request = new GraphQLRequest
        {
            Query = @"
                mutation ($mediaId: Int) {
                    DeleteMediaListEntry(id: $mediaId) {
                        deleted
                    }
                }",
            Variables = new { mediaId = animeId }
        };

        await _client.SendMutationAsync<object>(request);
    }

    public async Task SetAnimeStatusAsync(int animeId, AnimeStatus status)
    {
        await UpdateMediaListEntry(animeId, status: status);
    }

    public async Task SetAnimeScoreAsync(int animeId, int score)
    {
        await UpdateMediaListEntry(animeId, score: score);
    }

    public async Task SetEpisodesWatchedAsync(int animeId, int episodes)
    {
        await UpdateMediaListEntry(animeId, progress: episodes);
    }

    private async Task UpdateMediaListEntry(int mediaId, AnimeStatus? status = null, int? score = null, int? progress = null)
    {
        if (!_isLoggedIn) return;

        var variables = new Dictionary<string, object>
        {
            ["mediaId"] = mediaId
        };

        if (status.HasValue)
            variables["status"] = ConvertToAnilistStatus(status.Value);
        if (score.HasValue)
            variables["score"] = score.Value;
        if (progress.HasValue)
            variables["progress"] = progress.Value;

        var request = new GraphQLRequest
        {
            Query = @"
                mutation ($mediaId: Int, $status: MediaListStatus, $score: Float, $progress: Int) {
                    SaveMediaListEntry(mediaId: $mediaId, status: $status, score: $score, progress: $progress) {
                        id
                        status
                        score
                        progress
                    }
                }",
            Variables = variables
        };

        await _client.SendMutationAsync<object>(request);
    }

    public async Task<AnimeDetails?> FetchAnimeDetailsAsync(int animeId, params AnimeField[] fields)
    {
        Dictionary<string, object> variables = new() { { "id", animeId } };

        if (_isLoggedIn)
        {
            if (_currentUserId == null) _currentUserId = (await GetUserDataAsync()).Id;
            variables.Add("userId", _currentUserId);
        }
        
        var request = new GraphQLRequest
        {
            Query = @"
                query ($id: Int, $userId: Int) {
                    Media(id: $id, type: ANIME) {
                        id
                        title { romaji english native }
                        coverImage { large medium }
                        bannerImage
                        status
                        description
                        episodes
                        meanScore
                        popularity
                        favourites
                        startDate { year month day }
                        genres
                        
                        # Get specific user status if logged in
                        mediaListEntry(userId: $userId) {
                            status
                            score(format: POINT_10)
                            progress
                        }

                        studios(isMain: true) {
                            nodes { id name }
                        }

                        trailer { id site }

                        stats {
                            statusDistribution { status amount }
                        }

                        relations {
                            edges {
                                relationType
                                node {
                                    id
                                    title { romaji english }
                                    coverImage { medium }
                                    episodes
                                    status
                                    meanScore
                                }
                            }
                        }
                    }
                }",
            Variables = variables
        };

        try 
        {
            var response = await _client.SendQueryAsync<MediaResponse>(request);
            if (response.Data?.Media == null) return null;
            return await ConvertAnilistToUnified(response.Data.Media);
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<AnimeDetails>> SearchAnimeAsync(string query)
    {
        var request = new GraphQLRequest
        {
            Query = @"
                query ($search: String) {
                    Page(page: 1, perPage: 20) {
                        media(search: $search, type: ANIME) {
                            id
                            title {
                                romaji
                                english
                                native
                            }
                            coverImage {
                                large
                                medium
                            }
                            status
                            description
                            episodes
                            meanScore
                            popularity
                            studios {
                                nodes {
                                    id
                                    name
                                }
                            }
                            startDate {
                                year
                                month
                                day
                            }
                            genres
                        }
                    }
                }",
            Variables = new { search = query }
        };

        var response = await _client.SendQueryAsync<PageResponse>(request);
        var results = new List<AnimeDetails>();

        if (response.Data?.Page?.media != null)
        {
            foreach (var media in response.Data.Page.media)
            {
                results.Add(await ConvertAnilistToUnified(media));
            }
        }

        return results;
    }

    public async Task<List<RankingEntry>> GetTopAnimeAsync(RankingCategory category, int limit = 10)
    {
        var (sort, status) = category switch
        {
            RankingCategory.Airing => ("TRENDING_DESC", "RELEASING"),
            RankingCategory.Upcoming => ("POPULARITY_DESC", "NOT_YET_RELEASED"),
            RankingCategory.ByPopularity => ("POPULARITY_DESC", null),
            _ => ("SCORE_DESC", null)
        };

        var request = new GraphQLRequest
        {
            Query = @"
                query ($sort: [MediaSort], $status: MediaStatus, $perPage: Int) {
                    Page(page: 1, perPage: $perPage) {
                        media(type: ANIME, sort: $sort, status: $status) {
                            id
                            title {
                                romaji
                                english
                                native
                            }
                            coverImage {
                                large
                                medium
                            }
                            status
                            description
                            episodes
                            meanScore
                            popularity
                            studios {
                                nodes {
                                    id
                                    name
                                }
                            }
                            startDate {
                                year
                                month
                                day
                            }
                            genres
                        }
                    }
                }",
            Variables = new
            {
                sort = sort,
                status = status,
                perPage = limit
            }
        };

        var response = await _client.SendQueryAsync<PageResponse>(request);
        var results = new List<RankingEntry>();

        if (response.Data?.Page?.media != null)
        {
            int rank = 1;
            foreach (var media in response.Data.Page.media)
            {
                results.Add(new RankingEntry
                {
                    Details = await ConvertAnilistToUnified(media),
                    Rank = rank++
                });
            }
        }

        return results;
    }

    public async Task<Bitmap?> LoadAnimeImageAsync(int animeId, string? imageUrl)
    {
        if (_saveService.TryGetAnimeImage(animeId, out Bitmap? bitmap))
        {
            return bitmap;
        }

        if (!string.IsNullOrEmpty(imageUrl))
        {
            try
            {
                var imageData = await _httpClient.GetByteArrayAsync(imageUrl);
                using var ms = new MemoryStream(imageData);
                var downloadedImage = new Bitmap(ms);
                _saveService.SaveImage(animeId, downloadedImage);
                return downloadedImage;
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private async Task<AnimeDetails> ConvertAnilistToUnified(AnilistMedia media)
    {
        var picture = await LoadAnimeImageAsync(media.id, media.coverImage?.large);
        
        UserAnimeStatus? userStatus = null;
        if (media.mediaListEntry != null)
        {
            userStatus = new UserAnimeStatus
            {
                Status = ConvertFromAnilistStatus(media.mediaListEntry.status),
                Score = (int)(media.mediaListEntry.score ?? 0),
                EpisodesWatched = media.mediaListEntry.progress ?? 0
            };
        }
        
        var stats = new AnimeStatistics
        {
            NumListUsers = media.popularity ?? 0, 
            StatusStats = new StatusStatistics()
        };
        
        if (media.stats?.statusDistribution != null)
        {
            foreach (var dist in media.stats.statusDistribution)
            {
                switch (dist.status)
                {
                    case "CURRENT": stats.StatusStats.Watching = dist.amount; break;
                    case "COMPLETED": stats.StatusStats.Completed = dist.amount; break;
                    case "PAUSED": stats.StatusStats.OnHold = dist.amount; break;
                    case "DROPPED": stats.StatusStats.Dropped = dist.amount; break;
                    case "PLANNING": stats.StatusStats.PlanToWatch = dist.amount; break;
                }
            }
        }
        
        var relatedList = new List<RelatedAnime>();
        if (media.relations?.edges != null)
        {
            foreach (var edge in media.relations.edges)
            {
                if (edge.node == null) continue;
                
                relatedList.Add(new RelatedAnime
                {
                    RelationType = ConvertRelationType(edge.relationType),
                    Details = new AnimeDetails
                    {
                        Id = edge.node.id,
                        Title = edge.node.title?.romaji ?? edge.node.title?.english,
                        MainPicture = new AnimePicture 
                        { 
                            Medium = edge.node.coverImage?.medium ?? "",
                            Large = edge.node.coverImage?.large ?? "" // Fallback if needed
                        },
                        NumEpisodes = edge.node.episodes,
                        Status = edge.node.status, // Keep raw string or convert
                        Mean = (edge.node.meanScore ?? 0) / 10f
                    }
                });
            }
        }

        return new AnimeDetails(
            id: media.id,
            title: media.title?.romaji ?? media.title?.english ?? "Unknown Title",
            mainPicture: media.coverImage != null ? new AnimePicture
            {
                Medium = media.coverImage.medium ?? "",
                Large = media.coverImage.large ?? ""
            } : null,
            status: media.status, // You might want to normalize this string to "Finished Airing" etc.
            synopsis: media.description,
            alternativeTitles: new AlternativeTitles
            {
                English = media.title?.english,
                Japanese = media.title?.native,
                Synonyms = null 
            },
            userStatus: userStatus, // NOW POPULATED
            numEpisodes: media.episodes,
            popularity: media.popularity,
            picture: picture,
            studios: media.studios?.nodes?.Select(s => s.name).ToArray() ?? Array.Empty<string>(),
            startDate: FormatDate(media.startDate),
            mean: (media.meanScore ?? 0) / 10f,
            genres: media.genres?.ToArray(),
            trailerUrl: GetTrailerUrl(media.trailer),
            numFavorites: media.favourites,
            videos: null, // Anilist doesn't have a direct equivalent to MAL's PV list in the main object
            relatedAnime: relatedList.ToArray(), // NOW POPULATED
            statistics: stats // NOW POPULATED
        );
    }
    
    private string ConvertRelationType(string? type)
    {
        if (string.IsNullOrEmpty(type)) return "Related";
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(type.Replace("_", " ").ToLower());
    }

    private string? FormatDate(AnilistDate? date)
    {
        if (date?.year == null) return null;
        return $"{date.year:D4}-{date.month:D2}-{date.day:D2}";
    }

    private string? GetTrailerUrl(AnilistTrailer? trailer)
    {
        if (trailer == null) return null;
        
        return trailer.site?.ToLower() switch
        {
            "youtube" => $"https://www.youtube.com/watch?v={trailer.id}",
            _ => null
        };
    }

    private string ConvertToAnilistStatus(AnimeStatus status)
    {
        return status switch
        {
            AnimeStatus.Watching => "CURRENT",
            AnimeStatus.Completed => "COMPLETED",
            AnimeStatus.OnHold => "PAUSED",
            AnimeStatus.Dropped => "DROPPED",
            AnimeStatus.PlanToWatch => "PLANNING",
            _ => "PLANNING"
        };
    }

    private AnimeStatus ConvertFromAnilistStatus(string? status)
    {
        return status switch
        {
            "CURRENT" => AnimeStatus.Watching,
            "COMPLETED" => AnimeStatus.Completed,
            "PAUSED" => AnimeStatus.OnHold,
            "DROPPED" => AnimeStatus.Dropped,
            "PLANNING" => AnimeStatus.PlanToWatch,
            "REPEATING" => AnimeStatus.Watching,
            _ => AnimeStatus.None
        };
    }
}