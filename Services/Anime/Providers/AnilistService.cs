using System.Diagnostics;
using System.Net.Http.Headers;
using Aniki.Models.Anilist;
using Aniki.Services.Auth;
using Aniki.Services.Interfaces;
using Avalonia.Media.Imaging;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;

namespace Aniki.Services.Anime.Providers;

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
        if(response?.Data?.Viewer == null) return new UserData();

        return new UserData
        {
            Id = response.Data.Viewer.Id,
            Name = response.Data.Viewer.Name,
            Picture = response.Data.Viewer.Avatar?.Large
        };
    }
    
    public async Task<List<AnimeDetails>> GetUserAnimeListAsync(AnimeStatus statusFilter = AnimeStatus.None)
    {
        if (!_isLoggedIn) return new List<AnimeDetails>();

        HashSet<AnimeField> allFields = new(Enum.GetValues<AnimeField>());

        string myListStatusFormated;
        if (allFields.Contains(AnimeField.MyListStatus))
        {
            allFields.Remove(AnimeField.MyListStatus);
            myListStatusFormated = "\nstatus\nprogress\nscore(format: POINT_10)\n";
        }
        else
        {
            myListStatusFormated = "\n";
        }
        IEnumerable<string> fragments = GetGraphQlFragments(allFields);
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

        GraphQLResponse<Anilist_MediaListCollectionResponse>? response = await TrySendQueryAsync<Anilist_MediaListCollectionResponse>(request, "GetUserAnimeListAsync");
        if (response?.Data?.MediaListCollection?.Lists == null) return [];

        List<AnimeDetails> animeList = new();
        foreach (Anilist_MediaList list in response.Data.MediaListCollection.Lists)
        {
            if (list.Entries == null) continue;
            foreach (Anilist_MediaListEntry entry in list.Entries)
            {
                Anilist_MediaListStatus mediaListStatus = new()
                {
                    Progress = entry.Progress,
                    Score = entry.Score,
                    Status = entry.Status
                };
                animeList.Add(await ConvertAnilistToUnified(entry.Media, mediaListStatus));
            }
        }
        return animeList;
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

        GraphQLResponse<Anilist_MediaListResponse>? queryResponse = await TrySendQueryAsync<Anilist_MediaListResponse>(queryRequest, "RemoveFromUserListAsync");
        if (queryResponse?.Data?.AnilistMediaList == null) return;

        int entryId = queryResponse.Data.AnilistMediaList.Id;

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
        fragments.AddRange(GetGraphQlFragments(fields.ToHashSet()));

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

        GraphQLResponse<Anilist_MediaResponse>? response = await TrySendQueryAsync<Anilist_MediaResponse>(request, $"FetchAnimeDetailsAsync {animeId} {string.Join(", ", fields)}");
        if (response?.Data?.Media == null) return null;
            
        return await ConvertAnilistToUnified(response.Data.Media, response.Data.Media.MediaListEntry);
    }
    
    public async Task<List<AnimeDetails>> SearchAnimeAsync(string query)
    {
        HashSet<AnimeField> allFields = new(Enum.GetValues<AnimeField>());
        IEnumerable<string> fragments = GetGraphQlFragments(allFields);
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

        GraphQLResponse<Anilist_PageResponse>? response = await TrySendQueryAsync<Anilist_PageResponse>(request, "SearchAnimeAsync");
        if (response?.Data?.Page?.Media == null) return [];
        List<AnimeDetails> results = [];

        foreach (Anilist_Anime media in response.Data.Page.Media)
        {
            results.Add(await ConvertAnilistToUnified(media, media.MediaListEntry));
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

        HashSet<AnimeField> allFields = new(AnimeService.MalNodeFieldTypes);
        IEnumerable<string> fragments = GetGraphQlFragments(allFields);

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

        GraphQLResponse<Anilist_PageResponse>? response = await TrySendQueryAsync<Anilist_PageResponse>(request, "GetTopAnimeAsync");
        if (response?.Data?.Page?.Media == null) return [];
            
        List<RankingEntry> results = new();

        foreach (Anilist_Anime media in response.Data.Page.Media)
        {
            results.Add(new RankingEntry
            {
                Details = await ConvertAnilistToUnified(media, media.MediaListEntry),
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

    private string? GetTrailerUrl(Anilist_Trailer? trailer)
    {
        if (trailer == null) return null;
        
        return trailer.Site?.ToLower() switch
        {
            "youtube" => $"https://www.youtube.com/watch?v={trailer.Id}",
            _ => null
        };
    }
    
    private async Task<AnimeDetails> ConvertAnilistToUnified(Anilist_Anime anime, Anilist_MediaListStatus? userStatus)
    {
        Bitmap? picture = await LoadAnimeImageAsync(anime.Id, anime.CoverImage?.Large);
        
        AnimeStatistics stats = new()
        {
            NumListUsers = anime.Popularity ?? 0, 
            StatusStats = new StatusStatistics()
        };
        
        if (anime.Stats?.StatusDistribution != null)
        {
            foreach (Anilist_StatusDistribution dist in anime.Stats.StatusDistribution)
            {
                switch (dist.Status)
                {
                    case "CURRENT": stats.StatusStats.Watching = dist.Amount; break;
                    case "COMPLETED": stats.StatusStats.Completed = dist.Amount; break;
                    case "PAUSED": stats.StatusStats.OnHold = dist.Amount; break;
                    case "DROPPED": stats.StatusStats.Dropped = dist.Amount; break;
                    case "PLANNING": stats.StatusStats.PlanToWatch = dist.Amount; break;
                }
            }
        }
        
        List<RelatedAnime> relatedList = new();
        if (anime.Relations?.Edges != null)
        {
            foreach (Anilist_RelationEdge edge in anime.Relations.Edges)
            {
                if (edge.Node == null) continue;
                
                relatedList.Add(new RelatedAnime
                {
                    Relation = ConvertRelationType(edge.RelationType),
                    Details = new AnimeDetails
                    {
                        Id = edge.Node.Id,
                        Title = edge.Node.Title?.Romaji ?? edge.Node.Title?.English,
                        MainPicture = new AnimePicture 
                        { 
                            Medium = edge.Node.CoverImage?.Medium ?? "",
                            Large = edge.Node.CoverImage?.Large ?? ""
                        },
                        NumEpisodes = edge.Node.Episodes,
                        Status = edge.Node.Status,
                        Mean = (edge.Node.MeanScore ?? 0) / 10f,
                        MediaType = ConvertStringToMediaType(edge.Node.Format)
                    }
                });
            }
        }

        UserAnimeStatus? userAnimeStatus = null;
        
        if (userStatus != null)
        {
            userAnimeStatus = new UserAnimeStatus
            {
                Status = ConvertFromAnilistStatus(userStatus.Status),
                Score = (int)(userStatus.Score ?? 0),
                EpisodesWatched = userStatus.Progress ?? 0
            };
        }

        AnimeVideo[]? videos = null;
        if (anime.Trailer != null && anime.Trailer.Thumbnail != null)
        {
            videos =
            [
                new AnimeVideo
                {
                    Title = "Trailer",
                    Url = GetTrailerUrl(anime.Trailer!) ?? "",
                    Thumbnail = anime.Trailer.Thumbnail ?? ""
                }
            ];
        }

        return new AnimeDetails(
            id: anime.Id,
            title: anime.Title?.Romaji ?? anime.Title?.English ?? "Unknown Title",
            mainPicture: anime.CoverImage != null ? new AnimePicture
            {
                Medium = anime.CoverImage.Medium ?? "",
                Large = anime.CoverImage.Large ?? ""
            } : null,
            status: anime.Status,
            synopsis: anime.Description,
            alternativeTitles: new AlternativeTitles
            {
                English = anime.Title?.English,
                Japanese = anime.Title?.Native,
                Synonyms = null 
            },
            userStatus: userAnimeStatus,
            numEpisodes: anime.Episodes ?? 0,
            popularity: anime.Popularity,
            picture: picture,
            studios: anime.Studios?.Nodes?.Select(s => s.Name).ToArray() ?? Array.Empty<string>(),
            startDate: anime.StartDate == null ? null : $"{anime.StartDate.Year:D4}-{anime.StartDate.Month:D2}-{anime.StartDate.Day:D2}",
            mean: (anime.MeanScore ?? 0) / 10f,
            genres: anime.Genres?.ToArray(),
            trailerUrl: GetTrailerUrl(anime.Trailer),
            numFavorites: anime.Favourites,
            videos: videos,
            relatedAnime: relatedList.ToArray(),
            statistics: stats,
            mediaType: ConvertStringToMediaType(anime.Format)
        );
    }
    
    private RelatedAnime.RelationType ConvertRelationType(string? type)
    {
        if (string.IsNullOrEmpty(type)) return RelatedAnime.RelationType.Other;
        return type switch
        {
            "PREQUEL" => RelatedAnime.RelationType.Prequel,
            "SEQUEL" => RelatedAnime.RelationType.Sequel,
            _ => RelatedAnime.RelationType.Other
        };
    }
    
    private IEnumerable<string> GetGraphQlFragments(HashSet<AnimeField> fields)
    {
        if (fields.Count > 0)
        {
            yield return "id";
        }

        foreach (AnimeField field in fields)
        {
            switch (field)
            {
                case AnimeField.Id:
                    yield return "id";
                    break;
                case AnimeField.Title:
                case AnimeField.AlterTitles:
                    if (fields.Contains(AnimeField.Title) || fields.Contains(AnimeField.AlterTitles))
                    {
                        yield return "title { romaji english native }";
                        fields.Remove(AnimeField.Title); 
                        fields.Remove(AnimeField.AlterTitles);
                    }
                    break;

                case AnimeField.MainPicture:
                case AnimeField.Picture:
                    if (fields.Contains(AnimeField.MainPicture) || fields.Contains(AnimeField.Picture))
                    {
                        yield return "coverImage { large medium }";
                        yield return "bannerImage";
                        
                        fields.Remove(AnimeField.MainPicture);
                        fields.Remove(AnimeField.Picture);
                    }
                    break;
                
                case AnimeField.Status:
                    yield return "status";
                    break;

                case AnimeField.Synopsis:
                    yield return "description";
                    break;

                case AnimeField.Episodes:
                    yield return "episodes";
                    break;

                case AnimeField.Popularity:
                    yield return "popularity";
                    break;

                case AnimeField.NumFav:
                    yield return "favourites";
                    break;

                case AnimeField.StartDate:
                    yield return "startDate { year month day }";
                    break;

                case AnimeField.Genres:
                    yield return "genres";
                    break;

                case AnimeField.Mean:
                    yield return "meanScore";
                    break;
                
                case AnimeField.MediaType:
                    yield return "format";
                    break;

                case AnimeField.MyListStatus:
                    yield return @" mediaListEntry {
                                        status
                                        progress
                                        score(format: POINT_10)
                                    }";
                    break;

                case AnimeField.Studios:
                    yield return @"studios(isMain: true) {
                                nodes { id name }
                            }";
                    break;

                case AnimeField.Videos:
                case AnimeField.TrailerUrl:
                    if (fields.Contains(AnimeField.Videos) || fields.Contains(AnimeField.TrailerUrl))
                    {
                        yield return "trailer { id site thumbnail}";
                        
                        fields.Remove(AnimeField.Videos);
                        fields.Remove(AnimeField.TrailerUrl);
                    }
                    break;

                case AnimeField.Stats:
                    yield return @"stats {
                                statusDistribution { status amount }
                            }";
                    break;

                case AnimeField.RelatedAnime:
                    yield return @"relations {
                                edges {
                                    relationType
                                    node {
                                        id
                                        title { romaji english }
                                        coverImage { medium }
                                        episodes
                                        status
                                        meanScore,
                                        format
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

    private MediaType ConvertStringToMediaType(string? mediaType)
    {
        return mediaType switch
        {
            "TV" => MediaType.TV,
            "TV_SHORT" => MediaType.TV_Short,
            "MOVIE" => MediaType.Movie,
            "SPECIAL" => MediaType.Special,
            "OVA" => MediaType.OVA,
            "ONA" => MediaType.ONA,
            "MUSIC" => MediaType.Music,
            "MANGA" => MediaType.Manga,
            "NOVEL" => MediaType.Novel,
            "ONE_SHOT" => MediaType.One_Shot,
            _ => MediaType.Unknown,
        };
    }
}