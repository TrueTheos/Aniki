using System.Diagnostics;
using System.Net.Http.Headers;
using Aniki.Models.Anilist;
using Aniki.Services.Anime;
using Aniki.Services.Auth;
using Aniki.Services.Interfaces;
using Avalonia.Media.Imaging;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using Newtonsoft.Json.Linq;

namespace Aniki.Services;

public class AnilistService : IAnimeProvider
{
    private readonly GraphQLHttpClient _client;
    private readonly HttpClient _httpClient;
    private readonly ISaveService _saveService;
    
    private bool _isLoggedIn;
    public bool IsLoggedIn => _isLoggedIn;
    public ILoginProvider.ProviderType Provider => ILoginProvider.ProviderType.AniList;
    
    private int? _currentUserId;

    public AnilistService(ISaveService saveService)
    {
        AnilistRateLimitHandler rateLimitHandler = new(new HttpClientHandler());
        
        HttpClient graphQlHttpClient = new(rateLimitHandler)
        {
            BaseAddress = new Uri("https://graphql.anilist.co")
        };

        GraphQLHttpClientOptions options = new()
        {
            EndPoint = new Uri("https://graphql.anilist.co")
        };
        
        _client = new GraphQLHttpClient(options, new SystemTextJsonSerializer(), graphQlHttpClient);
        
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

    private async Task<GraphQLResponse<TResponse>?> TrySendQueryAsync<TResponse>(
        GraphQLRequest request, string message, CancellationToken cancellationToken = default)
    {
        #if DEBUG
        Debug.WriteLine($"Request: {message}");
        #endif
    
        try
        {
            var response = await _client.SendQueryAsync<TResponse>(request, cancellationToken);
       
            if (response.Errors != null && response.Errors.Length > 0)
            {
                foreach (var error in response.Errors)
                {
                    Debug.WriteLine($"GraphQL Error: {error.Message}");
                }
                return null;
            }

            return response;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Exception in TrySendQueryAsync: {ex.Message}");
            return null;
        }
    }

    #region  IAnimeProvider

    public async Task<UserData> GetUserDataAsync()
    {
        if (!_isLoggedIn) return new UserData();

        GraphQLRequest request = new()
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

        GraphQLResponse<ViewerResponse>? response = await TrySendQueryAsync<ViewerResponse>(request, "GetUserDataAsync");
        if(response == null || response.Data?.Viewer == null) return new UserData();

        return new UserData
        {
            Id = response.Data.Viewer.id,
            Name = response.Data.Viewer.name,
            Picture = response.Data.Viewer.avatar?.large
        };
    }
    
    public async Task<List<AnimeDetails>> GetUserAnimeListAsync(AnimeStatus statusFilter = AnimeStatus.None)
    {
        if (!_isLoggedIn) return new List<AnimeDetails>();

        HashSet<AnimeField> allFields = new(Enum.GetValues<AnimeField>());

        string myListStatusFormated = "";
        if (allFields.Contains(AnimeField.MY_LIST_STATUS))
        {
            allFields.Remove(AnimeField.MY_LIST_STATUS);
            myListStatusFormated = "\nstatus\nprogress\nscore(format: POINT_10)\n";
        }
        else
        {
            myListStatusFormated = "\n";
        }
        IEnumerable<string> fragments = GetGraphQLFragments(allFields);
        string innerQuery = string.Join("\n", fragments);

        string statusText = statusFilter != AnimeStatus.None ? $", status: {ConvertToAnilistStatus(statusFilter)}" : "";
        
        GraphQLRequest request = new()
        {
            Query = @"
                query ($userId: Int) {
                    MediaListCollection(userId: $userId, type: ANIME" + statusText + @") {
                        lists {
                            entries {
                                id" + myListStatusFormated +
                                @"media {
                                    " + innerQuery + @"
                                }
                            }
                        }
                    }
                }",
            Variables = new
            {
                userId = _currentUserId,
            }
        };

        GraphQLResponse<MediaListCollectionResponse>? response = await TrySendQueryAsync<MediaListCollectionResponse>(request, "GetUserAnimeListAsync");
        if (response == null) return [];

        if (response.Data?.MediaListCollection?.lists != null)
        {
            List<AnimeDetails> animeList = new();
            foreach (MediaList list in response.Data.MediaListCollection.lists)
            {
                if (list.entries == null) continue;
                foreach (MediaListEntry entry in list.entries)
                {
                    AnilistMediaListStatus mediaListStatus = new()
                    {
                        progress = entry.progress,
                        score = entry.score,
                        status = entry.status
                    };
                    animeList.Add(await ConvertAnilistToUnified(entry.media, mediaListStatus));
                }
            }
            return animeList;
        }

        return [];
    }

    public async Task RemoveFromUserListAsync(int animeId)
    {
        if (!_isLoggedIn) return;

        GraphQLRequest queryRequest = new()
        {
            Query = @"
            query ($mediaId: Int, $userId: Int) {
                MediaList(mediaId: $mediaId, userId: $userId) {
                    id
                    status
                }
            }",
            Variables = new { mediaId = animeId, userId = _currentUserId }
        };

        GraphQLResponse<MediaListResponse>? queryResponse = await TrySendQueryAsync<MediaListResponse>(queryRequest, "RemoveFromUserListAsync");
        if (queryResponse?.Data?.MediaList == null) return;

        int entryId = queryResponse.Data.MediaList.id;

        GraphQLRequest deleteRequest = new()
        {
            Query = @"
            mutation ($id: Int) {
                DeleteMediaListEntry(id: $id) {
                    deleted
                }
            }",
            Variables = new { id = entryId }
        };

        await _client.SendMutationAsync<object>(deleteRequest);
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
    
    public async Task<AnimeDetails?> FetchAnimeDetailsAsync(int animeId, params AnimeField[] fields)
    {
        List<string> fragments = new() { "id" };
        fragments.AddRange(GetGraphQLFragments(fields.ToHashSet()));

        string query = $@"
            query ($id: Int) {{
                Media(id: $id, type: ANIME) {{
                    {string.Join("\n", fragments)}
                }}
            }}";

        GraphQLRequest request = new()
        {
            Query = query,
            Variables = new Dictionary<string, object> { { "id", animeId } }
        };

        GraphQLResponse<MediaResponse>? response = await TrySendQueryAsync<MediaResponse>(request, $"FetchAnimeDetailsAsync {animeId} {string.Join(", ", fields)}");
        if (response?.Data?.Media == null) return null;
            
        return await ConvertAnilistToUnified(response.Data.Media, response.Data.Media.mediaListEntry);
    }
    
    public async Task<List<AnimeDetails>> SearchAnimeAsync(string query)
    {
        HashSet<AnimeField> allFields = new(Enum.GetValues<AnimeField>());
        IEnumerable<string> fragments = GetGraphQLFragments(allFields);
        string innerQuery = string.Join("\n", fragments);

        GraphQLRequest request = new()
        {
            Query = $@"
                query ($search: String) {{
                    Page(page: 1, perPage: 20) {{
                        media(search: $search, type: ANIME) {{
                            {innerQuery}
                        }}
                    }}
                }}",
            Variables =  new Dictionary<string, object> { ["search"] = query }
        };

        GraphQLResponse<PageResponse>? response = await TrySendQueryAsync<PageResponse>(request, "SearchAnimeAsync");
        if (response?.Data?.Page?.media == null) return [];
        List<AnimeDetails> results = [];

        foreach (AnilistMedia media in response.Data.Page.media)
        {
            results.Add(await ConvertAnilistToUnified(media, media.mediaListEntry));
        }
            
        return results;
    }
    
    public async Task<List<RankingEntry>> GetTopAnimeAsync(RankingCategory category, int limit = 10)
    {
        (string? sort, string? status) = category switch
        {
            RankingCategory.Airing => ("TRENDING_DESC", "RELEASING"),
            RankingCategory.Upcoming => ("POPULARITY_DESC", "NOT_YET_RELEASED"),
            RankingCategory.ByPopularity => ("POPULARITY_DESC", null),
            _ => ("SCORE_DESC", null)
        };

        HashSet<AnimeField> allFields = new(AnimeService.MAL_NODE_FIELD_TYPES);
        IEnumerable<string> fragments = GetGraphQLFragments(allFields);

        Dictionary<string, object> variables = new()
        {
            ["sort"] = sort,
            ["perPage"] = limit
        };
        
        if (status != null)
        {
            variables["status"] = status;
        }
        
        string queryVariables = "($sort: [MediaSort], $perPage: Int";
        if (status != null)
        {
            queryVariables += ", $status: MediaStatus";
        }
        queryVariables += ")";
        
        string mediaArgs = "type: ANIME, sort: $sort";
        if (status != null)
        {
            mediaArgs += ", status: $status";
        }

        GraphQLRequest request = new()
        {
            Query = $@"
                query {queryVariables} {{
                    Page(page: 1, perPage: $perPage) {{
                        media({mediaArgs}) {{
                            {string.Join("\n", fragments)}
                        }}
                    }}
                }}",
            Variables = variables
        };

        GraphQLResponse<PageResponse>? response = await TrySendQueryAsync<PageResponse>(request, "GetTopAnimeAsync");
        if (response?.Data?.Page?.media == null) return [];
            
        List<RankingEntry> results = new();

        foreach (AnilistMedia media in response.Data.Page.media)
        {
            results.Add(new RankingEntry
            {
                Details = await ConvertAnilistToUnified(media, media.mediaListEntry),
            });
        }

        return results;
    }
    
    public async Task<Bitmap?> LoadAnimeImageAsync(int animeId, string? imageUrl)
    {
        if (_saveService.TryGetAnimeImage(animeId, out Bitmap? bitmap))
        {
            return bitmap;
        }

        if (string.IsNullOrEmpty(imageUrl)) return null;
        try
        {
            byte[] imageData = await _httpClient.GetByteArrayAsync(imageUrl);
            using MemoryStream ms = new(imageData);
            Bitmap downloadedImage = new(ms);
            _saveService.SaveImage(animeId, downloadedImage);
            return downloadedImage;
        }
        catch
        {
            return null;
        }
    }
    
    #endregion

    private async Task UpdateMediaListEntry(int mediaId, AnimeStatus? status = null, int? score = null, int? progress = null)
    {
        if (!_isLoggedIn) return;

        Dictionary<string, object> variables = new()
        {
            ["mediaId"] = mediaId
        };

        if (status.HasValue)
            variables["status"] = ConvertToAnilistStatus(status.Value);
        if (score.HasValue)
            variables["score"] = score.Value;
        if (progress.HasValue)
            variables["progress"] = progress.Value;

        GraphQLRequest request = new()
        {
            Query = @"
                mutation ($mediaId: Int, $status: MediaListStatus, $score: Float, $progress: Int) {
                    SaveMediaListEntry(mediaId: $mediaId, status: $status, score: $score, progress: $progress) {
                        id" + 
                        (status.HasValue ? "\nstatus\n" : "") +
                        (score.HasValue ? "\nscore\n" : "") +
                        (progress.HasValue ? "\nprogress\n" : "") +
                    @"}
                }",
            Variables = variables
        };

        await _client.SendMutationAsync<object>(request);
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
    
    private async Task<AnimeDetails> ConvertAnilistToUnified(AnilistMedia media, AnilistMediaListStatus? userStatus)
    {
        Bitmap? picture = await LoadAnimeImageAsync(media.id, media.coverImage?.large);
        
        AnimeStatistics stats = new()
        {
            NumListUsers = media.popularity ?? 0, 
            StatusStats = new StatusStatistics()
        };
        
        if (media.stats?.statusDistribution != null)
        {
            foreach (AnilistStatusDistribution dist in media.stats.statusDistribution)
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
        
        List<RelatedAnime> relatedList = new();
        if (media.relations?.edges != null)
        {
            foreach (AnilistRelationEdge edge in media.relations.edges)
            {
                if (edge.node == null) continue;
                
                relatedList.Add(new RelatedAnime
                {
                    Relation = ConvertRelationType(edge.relationType),
                    Details = new AnimeDetails
                    {
                        Id = edge.node.id,
                        Title = edge.node.title?.romaji ?? edge.node.title?.english,
                        MainPicture = new AnimePicture 
                        { 
                            Medium = edge.node.coverImage?.medium ?? "",
                            Large = edge.node.coverImage?.large ?? ""
                        },
                        NumEpisodes = edge.node.episodes,
                        Status = edge.node.status,
                        Mean = (edge.node.meanScore ?? 0) / 10f
                    }
                });
            }
        }

        UserAnimeStatus? userAnimeStatus = null;
        
        if (userStatus != null)
        {
            userAnimeStatus = new UserAnimeStatus
            {
                Status = ConvertFromAnilistStatus(userStatus.status),
                Score = (int)(userStatus.score ?? 0),
                EpisodesWatched = userStatus.progress ?? 0
            };
        }

        AnimeVideo[]? videos = null;
        if (media.trailer != null)
        {
            videos =
            [
                new AnimeVideo
                {
                    Title = "Trailer",
                    Url = GetTrailerUrl(media.trailer!) ?? "",
                    Thumbnail = media.trailer.thumbnail ?? ""
                }
            ];
        }

        return new AnimeDetails(
            id: media.id,
            title: media.title?.romaji ?? media.title?.english ?? "Unknown Title",
            mainPicture: media.coverImage != null ? new AnimePicture
            {
                Medium = media.coverImage.medium ?? "",
                Large = media.coverImage.large ?? ""
            } : null,
            status: media.status,
            synopsis: media.description,
            alternativeTitles: new AlternativeTitles
            {
                English = media.title?.english,
                Japanese = media.title?.native,
                Synonyms = null 
            },
            userStatus: userAnimeStatus,
            numEpisodes: media.episodes ?? 0,
            popularity: media.popularity,
            picture: picture,
            studios: media.studios?.nodes?.Select(s => s.name).ToArray() ?? Array.Empty<string>(),
            startDate: media.startDate == null ? null : $"{media.startDate.year:D4}-{media.startDate.month:D2}-{media.startDate.day:D2}",
            mean: (media.meanScore ?? 0) / 10f,
            genres: media.genres?.ToArray(),
            trailerUrl: GetTrailerUrl(media.trailer),
            numFavorites: media.favourites,
            videos: videos,
            relatedAnime: relatedList.ToArray(),
            statistics: stats
        );
    }
    
    private RelatedAnime.RelationType ConvertRelationType(string? type)
    {
        if (string.IsNullOrEmpty(type)) return RelatedAnime.RelationType.OTHER;
        return type switch
        {
            "PREQUEL" => RelatedAnime.RelationType.PREQUEL,
            "SEQUEL" => RelatedAnime.RelationType.SEQUEL,
            _ => RelatedAnime.RelationType.OTHER
        };
    }
    
    private IEnumerable<string> GetGraphQLFragments(HashSet<AnimeField> fields)
    {
        if (fields.Count > 0)
        {
            yield return "id";
        }

        foreach (AnimeField field in fields)
        {
            switch (field)
            {
                case AnimeField.ID:
                    yield return "id";
                    break;
                case AnimeField.TITLE:
                case AnimeField.ALTER_TITLES:
                    if (fields.Contains(AnimeField.TITLE) || fields.Contains(AnimeField.ALTER_TITLES))
                    {
                        yield return "title { romaji english native }";
                        fields.Remove(AnimeField.TITLE); 
                        fields.Remove(AnimeField.ALTER_TITLES);
                    }
                    break;

                case AnimeField.MAIN_PICTURE:
                case AnimeField.PICTURE:
                    if (fields.Contains(AnimeField.MAIN_PICTURE) || fields.Contains(AnimeField.PICTURE))
                    {
                        yield return "coverImage { large medium }";
                        yield return "bannerImage";
                        
                        fields.Remove(AnimeField.MAIN_PICTURE);
                        fields.Remove(AnimeField.PICTURE);
                    }
                    break;
                
                case AnimeField.STATUS:
                    yield return "status";
                    break;

                case AnimeField.SYNOPSIS:
                    yield return "description";
                    break;

                case AnimeField.EPISODES:
                    yield return "episodes";
                    break;

                case AnimeField.POPULARITY:
                    yield return "popularity";
                    break;

                case AnimeField.NUM_FAV:
                    yield return "favourites";
                    break;

                case AnimeField.START_DATE:
                    yield return "startDate { year month day }";
                    break;

                case AnimeField.GENRES:
                    yield return "genres";
                    break;

                case AnimeField.MEAN:
                    yield return "meanScore";
                    break;

                case AnimeField.MY_LIST_STATUS:
                    yield return @" mediaListEntry {
                                        status
                                        progress
                                        score(format: POINT_10)
                                    }";
                    break;

                case AnimeField.STUDIOS:
                    yield return @"studios(isMain: true) {
                                nodes { id name }
                            }";
                    break;

                case AnimeField.VIDEOS:
                case AnimeField.TRAILER_URL:
                    if (fields.Contains(AnimeField.VIDEOS) || fields.Contains(AnimeField.TRAILER_URL))
                    {
                        yield return "trailer { id site thumbnail}";
                        
                        fields.Remove(AnimeField.VIDEOS);
                        fields.Remove(AnimeField.TRAILER_URL);
                    }
                    break;

                case AnimeField.STATS:
                    yield return @"stats {
                                statusDistribution { status amount }
                            }";
                    break;

                case AnimeField.RELATED_ANIME:
                    yield return @"relations {
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
                            }";
                    break;
                
                default:
                    Console.WriteLine($"{field} is not supported");
                    break;
            }
        }
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