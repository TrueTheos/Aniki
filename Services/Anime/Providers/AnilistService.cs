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
    private int? _currentUserId;

    public ILoginProvider.ProviderType Provider => ILoginProvider.ProviderType.AniList;
    public bool IsLoggedIn => _isLoggedIn;
    
    private int _requestCount = 0;

    public AnilistService(ISaveService saveService)
    {
        var rateLimitHandler = new AnilistRateLimitHandler(new HttpClientHandler());
        
        var graphQlHttpClient = new HttpClient(rateLimitHandler)
        {
            BaseAddress = new Uri("https://graphql.anilist.co")
        };

        var options = new GraphQLHttpClientOptions
        {
            EndPoint = new Uri("https://graphql.anilist.co")
        };
        
        _client = new GraphQLHttpClient(options, new SystemTextJsonSerializer(), graphQlHttpClient);
        
        _httpClient = new HttpClient();
        _saveService = saveService;
    }

    private async Task<GraphQLResponse<TResponse>> SendQueryAsyncWrapper<TResponse>(GraphQLRequest request, string message,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"{_requestCount}: {message}");
        _requestCount++;
        return await _client.SendQueryAsync<TResponse>(request, cancellationToken);
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

        var response = await SendQueryAsyncWrapper<ViewerResponse>(request, "GetUserDataAsync");

        if (response.Data?.Viewer == null)
            throw new InvalidOperationException("Failed to get user data");

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

        var allFields = new HashSet<AnimeField>((AnimeField[])Enum.GetValues(typeof(AnimeField)));

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
        var fragments = GetGraphQLFragments(allFields);
        var innerQuery = string.Join("\n", fragments);

        string statusText = statusFilter != AnimeStatus.None ? $", status: {ConvertToAnilistStatus(statusFilter)}" : "";
        
        var request = new GraphQLRequest
        {
            Query = @"
                query ($userId: Int" + (statusFilter != AnimeStatus.None ? ", $status: MediaListStatus" : "") + @") {
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
            Variables = statusFilter != AnimeStatus.None ? new
            {
                userId = _currentUserId,
            } : new
            {
                userId = _currentUserId,
                status = statusFilter
            }
        };

        var response = await SendQueryAsyncWrapper<MediaListCollectionResponse>(request, "GetUserAnimeListAsync");
        var animeList = new List<AnimeDetails>();

        if (response.Data?.MediaListCollection?.lists != null)
        {
            foreach (var list in response.Data.MediaListCollection.lists)
            {
                if (list.entries != null)
                {
                    foreach (var entry in list.entries)
                    {
                        AnilistMediaListStatus mediaListStatus = new AnilistMediaListStatus()
                        {
                            progress = entry.progress,
                            score = entry.score,
                            status = entry.status
                        };
                        animeList.Add(await ConvertAnilistToUnified(entry.media, mediaListStatus));
                    }
                }
            }
        }

        return animeList;
    }

    private class MediaListResponse
    {
        public MediaListEntry MediaList { get; set; } = null!;
    }
    
    public async Task RemoveFromUserListAsync(int animeId)
    {
        if (!_isLoggedIn) return;

        var queryRequest = new GraphQLRequest
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

        var queryResponse = await SendQueryAsyncWrapper<MediaListResponse>(queryRequest, "RemoveFromUserListAsync");

        if (queryResponse.Data?.MediaList == null) return;

        int entryId = queryResponse.Data.MediaList.id;

        var deleteRequest = new GraphQLRequest
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

    public async Task<AnimeDetails?> FetchAnimeDetailsAsync(int animeId, params AnimeField[] fields)
    {
        Dictionary<string, object> variables = new() { { "id", animeId } };

        var fragments = new List<string> {
            "id"
        };
    
        var fieldSet = new HashSet<AnimeField>(fields);

        fragments.AddRange(GetGraphQLFragments(fieldSet));
        
        string innerQuery = string.Join("\n", fragments);
    
        string query = $@"
        query ($id: Int) {{
            Media(id: $id, type: ANIME) {{
                {innerQuery}
            }}
        }}";

        var request = new GraphQLRequest
        {
            Query = query,
            Variables = variables
        };

        try 
        {
            var response = await SendQueryAsyncWrapper<MediaResponse>(request, $"FetchAnimeDetailsAsync {animeId} {string.Join(", ", fields)}");
            if (response.Data?.Media == null) return null;
            return await ConvertAnilistToUnified(response.Data.Media, response.Data.Media.mediaListEntry);
        }
        catch
        {
            return null;
        }
    }

    private IEnumerable<string> GetGraphQLFragments(HashSet<AnimeField> fields)
    {
        if (fields.Count > 0)
        {
            yield return "id";
        }

        foreach (var field in fields)
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
      
    public async Task<List<AnimeDetails>> SearchAnimeAsync(string query)
    {
        var allFields = new HashSet<AnimeField>((AnimeField[])Enum.GetValues(typeof(AnimeField)));
        var fragments = GetGraphQLFragments(allFields);
        var innerQuery = string.Join("\n", fragments);

        var variables = new Dictionary<string, object> { ["search"] = query };
        
        var queryVariables = "($search: String)";

        var request = new GraphQLRequest
        {
            Query = $@"
                query {queryVariables} {{
                    Page(page: 1, perPage: 20) {{
                        media(search: $search, type: ANIME) {{
                            {innerQuery}
                        }}
                    }}
                }}",
            Variables = variables
        };

        var response = await SendQueryAsyncWrapper<PageResponse>(request, "SearchAnimeAsync");
        var results = new List<AnimeDetails>();

        if (response.Data?.Page?.media != null)
        {
            foreach (var media in response.Data.Page.media)
            {
                results.Add(await ConvertAnilistToUnified(media, media.mediaListEntry));
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

        var allFields = new HashSet<AnimeField>(AnimeService.MAL_NODE_FIELD_TYPES);
        var fragments = GetGraphQLFragments(allFields);
        var innerQuery = string.Join("\n", fragments);

        var variables = new Dictionary<string, object>
        {
            ["sort"] = sort,
            ["perPage"] = limit
        };
        if (status != null)
        {
            variables["status"] = status;
        }
        
        var queryVariables = "($sort: [MediaSort], $perPage: Int";
        if (status != null)
        {
            queryVariables += ", $status: MediaStatus";
        }
        queryVariables += ")";
        
        var mediaArgs = "type: ANIME, sort: $sort";
        if (status != null)
        {
            mediaArgs += ", status: $status";
        }

        var request = new GraphQLRequest
        {
            Query = $@"
                query {queryVariables} {{
                    Page(page: 1, perPage: $perPage) {{
                        media({mediaArgs}) {{
                            {innerQuery}
                        }}
                    }}
                }}",
            Variables = variables
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
                    
                    Details = await ConvertAnilistToUnified(media, media.mediaListEntry),
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

    private async Task<AnimeDetails> ConvertAnilistToUnified(AnilistMedia media, AnilistMediaListStatus? userStatus)
    {
        var picture = await LoadAnimeImageAsync(media.id, media.coverImage?.large);
        
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

        AnimeVideo[]? vidoes = null;
        if (media.trailer != null)
        {
            vidoes = new[]
            {
                new AnimeVideo
                {
                    Title = "Trailer",
                    Url = GetTrailerUrl(media.trailer!) ?? "",
                    Thumbnail = media.trailer.thumbnail ?? ""
                }
            };
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
            startDate: FormatDate(media.startDate),
            mean: (media.meanScore ?? 0) / 10f,
            genres: media.genres?.ToArray(),
            trailerUrl: GetTrailerUrl(media.trailer),
            numFavorites: media.favourites,
            videos: vidoes,
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