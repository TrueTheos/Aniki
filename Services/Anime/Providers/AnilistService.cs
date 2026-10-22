using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Aniki.Misc;
using Aniki.Models.Anilist;
using Aniki.Models.Anilist.Components;
using Aniki.Services.Auth;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;

namespace Aniki.Services.Anime.Providers;

internal sealed class AnilistService : IAnimeProvider, IDisposable
{
    private sealed record AiringPageResponse(AiringPage? Page);
    private sealed record AiringPage(List<AiringMedia>? Media);
    private sealed record AiringMedia(int? Id, int? IdMal, int? Episodes, NextAiringEpisode? NextAiringEpisode);
    private sealed record NextAiringEpisode(int Episode);

    private sealed record DiscoverPageResponse(DiscoverPage? Page);
    private sealed record DiscoverPage(DiscoverPageInfo? PageInfo, List<Anilist_Anime>? Media);
    private sealed record DiscoverPageInfo(bool HasNextPage);
    private sealed record TagCollectionResponse(List<TagItem>? MediaTagCollection);
    private sealed record TagItem(string Name, bool IsAdult);

    private List<string>? _tags;
    
    private readonly GraphQLHttpClient _client;

    public bool IsLoggedIn { get; private set; }

    public ILoginProvider.ProviderType Provider => ILoginProvider.ProviderType.AniList;

    private int? _currentUserId;

    private readonly MediaKind _kind;
    private bool IsManga => _kind == MediaKind.Manga;
    private string Type => IsManga ? "MANGA" : "ANIME";

    public AnilistService(MediaKind kind = MediaKind.Anime)
    {
        _kind = kind;
#pragma warning disable CA2000
        AnilistRateLimitHandler rateLimitHandler = new(new HttpClientHandler());
#pragma warning restore CA2000
        
#pragma warning disable CA2000
        HttpClient graphQlHttpClient = new(rateLimitHandler)
        {
            BaseAddress = new Uri("https://graphql.anilist.co")
        };
#pragma warning restore CA2000

        GraphQLHttpClientOptions options = new()
        {
            EndPoint = new Uri("https://graphql.anilist.co")
        };
        
        _client = new GraphQLHttpClient(options, new SystemTextJsonSerializer(), graphQlHttpClient);
    }
    
    public IAnimeProvider ForManga() => new AnilistService(MediaKind.Manga);

    public async Task InitAsync(string? accessToken)
    {
        if (!string.IsNullOrEmpty(accessToken))
        {
            _client.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            IsLoggedIn = true;
            UserData userData = await GetUserDataAsync().ConfigureAwait(false);
            _currentUserId = userData.Id;
        }
        else
        {
            IsLoggedIn = false;
            _currentUserId = null;
        }
    }

    private async Task<GraphQLResponse<TResponse>?> TrySendQueryAsync<TResponse>(
        GraphQLRequest request, string message, CancellationToken cancellationToken = default)
    {
        string? previous = AnilistRequestContext.Operation;
        AnilistRequestContext.Operation = message;
        try
        {
            var response = await _client.SendQueryAsync<TResponse>(request, cancellationToken).ConfigureAwait(false);

            if (response.Errors != null && response.Errors.Length > 0)
            {
                foreach (GraphQLError error in response.Errors)
                {
                    Console.WriteLine($"[AL] GraphQL Error in {message}: {error.Message}");
                }
                return null;
            }

            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AL] Exception in {message}: {ex.Message}");
            return null;
        }
        finally
        {
            AnilistRequestContext.Operation = previous;
        }
    }
    
    private async Task SendMutationAsync(GraphQLRequest request, string message)
    {
        var response = await _client.SendMutationAsync<object>(request).ConfigureAwait(false);
        if (response.Errors is { Length: > 0 })
            throw new InvalidOperationException($"AniList {message} failed: {response.Errors[0].Message}");
    }

    public async Task<Dictionary<int, int>> GetReleasedEpisodeCountsAsync(
        IReadOnlyList<int> ids, bool useMalIds = true)
    {
        Dictionary<int, int> results = new();
        List<int> distinctIds = ids.Distinct().ToList();

        const int pageSize = 50;
        for (int offset = 0; offset < distinctIds.Count; offset += pageSize)
        {
            List<int> chunk = distinctIds.Skip(offset).Take(pageSize).ToList();
            string idFilter = useMalIds ? "idMal_in" : "id_in";
            string idField = useMalIds ? "idMal" : "id";

            GraphQLRequest request = new()
            {
                Query = $$"""
                    query ($ids: [Int]) {
                        Page(page: 1, perPage: {{pageSize}}) {
                            media({{idFilter}}: $ids, type: ANIME) {
                                {{idField}}
                                episodes
                                nextAiringEpisode {
                                    episode
                                }
                            }
                        }
                    }
                    """,
                Variables = new { ids = chunk }
            };

            var response = await TrySendQueryAsync<AiringPageResponse>(
                request, $"GetReleasedEpisodeCountsAsync {chunk.Count}").ConfigureAwait(false);

            foreach (AiringMedia media in response?.Data?.Page?.Media ?? [])
            {
                int? key = useMalIds ? media.IdMal : media.Id;
                if (key is not { } id) continue;

                int released = media.NextAiringEpisode?.Episode is { } next
                    ? next - 1
                    : media.Episodes ?? 0;

                results[id] = released;
            }
        }

        return results;
    }

    public async Task<int?> GetSourceMangaIdAsync(int animeId, bool useMalId)
    {
        string idArg = useMalId ? "idMal" : "id";
        GraphQLRequest request = new()
        {
            Query = $"query ($id: Int) {{ Media({idArg}: $id, type: ANIME) {{ relations {{ edges {{ relationType node {{ id idMal type format }} }} }} }} }}",
            Variables = new { id = animeId }
        };

        var response = await TrySendQueryAsync<Anilist_MediaResponse>(request, $"GetSourceMangaIdAsync {animeId}").ConfigureAwait(false);
        Anilist_RelationEdge? edge = response?.Data?.Media?.Relations?.Edges?
            .Where(e => e.Node?.Type == "MANGA" && (!useMalId || e.Node.IdMal != null))
            .OrderBy(e => e.Node!.Format switch { "MANGA" => 0, "NOVEL" => 2, _ => 1 })
            .FirstOrDefault();
        return useMalId ? edge?.Node?.IdMal : edge?.Node?.Id;
    }

    public async Task<(List<Anilist_Anime> Items, bool HasMore)> DiscoverAsync(
        MediaKind kind, IReadOnlyList<string> genres, IReadOnlyList<string> tags,
        string? format, string? country, string? status, string sort, int page)
    {
        Dictionary<string, object> variables = new()
        {
            ["page"] = page,
            ["type"] = kind == MediaKind.Manga ? "MANGA" : "ANIME",
            ["sort"] = sort
        };
        if (genres.Count > 0) variables["genres"] = genres;
        if (tags.Count > 0) variables["tags"] = tags;
        if (format != null) variables["format"] = format;
        if (country != null) variables["country"] = country;
        if (status != null) variables["status"] = status;

        GraphQLRequest request = new()
        {
            Query = @"
                query ($page: Int, $type: MediaType, $genres: [String], $tags: [String], $format: MediaFormat,
                       $country: CountryCode, $status: MediaStatus, $sort: [MediaSort]) {
                    Page(page: $page, perPage: 30) {
                        pageInfo { hasNextPage }
                        media(type: $type, genre_in: $genres, tag_in: $tags, format: $format, countryOfOrigin: $country,
                              status: $status, sort: $sort, isAdult: false) {
                            id idMal title { romaji english } coverImage { large medium } meanScore
                        }
                    }
                }",
            Variables = variables
        };

        var response = await TrySendQueryAsync<DiscoverPageResponse>(request, $"DiscoverAsync page {page}").ConfigureAwait(false);
        return (response?.Data?.Page?.Media ?? [], response?.Data?.Page?.PageInfo?.HasNextPage ?? false);
    }

    public async Task<List<string>> GetTagsAsync()
    {
        if (_tags != null) return _tags;

        GraphQLRequest request = new() { Query = "query { MediaTagCollection { name isAdult } }" };
        var response = await TrySendQueryAsync<TagCollectionResponse>(request, "GetTagsAsync").ConfigureAwait(false);
        List<string> tags = response?.Data?.MediaTagCollection?.Where(t => !t.IsAdult).Select(t => t.Name).Order().ToList() ?? [];
        if (tags.Count > 0) _tags = tags;
        return tags;
    }

    #region  IAnimeProvider

    public async Task<UserData> GetUserDataAsync()
    {
        if (!IsLoggedIn) return new UserData();

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

        var response = await TrySendQueryAsync<ViewerResponse>(request, "GetUserDataAsync").ConfigureAwait(false);
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
        if (!IsLoggedIn) return [];

        HashSet<AnimeField> fields = [..AnimeService.MalNodeFieldTypes];
        fields.Remove(AnimeField.MyListStatus);
        string innerQuery = string.Join("\n", GetGraphQlFragments(fields));

        string statusText = statusFilter switch
        {
            AnimeStatus.None => "",
            AnimeStatus.Watching => ", status_in: [CURRENT, REPEATING]",
            _ => $", status: {ConvertToAnilistStatus(statusFilter)}"
        };

        GraphQLRequest request = new()
        {
            Query = @"
                query ($userId: Int) {
                    MediaListCollection(userId: $userId, type: " + Type + statusText + @") {
                        lists {
                            isCustomList
                            entries {
                                id
                                status
                                progress
                                score(format: POINT_10)
                                media {
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

        var response = await TrySendQueryAsync<Anilist_MediaListCollectionResponse>(request, "GetUserAnimeListAsync").ConfigureAwait(false);
        if (response?.Data?.MediaListCollection?.Lists == null) return [];

        List<AnimeDetails> animeList = [];
        foreach (Anilist_MediaList list in response.Data.MediaListCollection.Lists)
        {
            if (list.Entries == null || list.IsCustomList) continue;
            foreach (Anilist_MediaListEntry entry in list.Entries)
            {
                Anilist_MediaListStatus mediaListStatus = new()
                {
                    Progress = entry.Progress,
                    Score = entry.Score,
                    Status = entry.Status
                };
                animeList.Add(ConvertAnilistToUnified(entry.Media, mediaListStatus));
            }
        }
        return animeList;
    }

    public async Task RemoveFromUserListAsync(int animeId)
    {
        if (!IsLoggedIn) return;

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

        var queryResponse = await TrySendQueryAsync<Anilist_MediaListResponse>(queryRequest, "RemoveFromUserListAsync").ConfigureAwait(false);
        if (queryResponse?.Data?.MediaList == null) return;

        int entryId = queryResponse.Data.MediaList.Id;

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

        await SendMutationAsync(deleteRequest, "RemoveFromUserListAsync").ConfigureAwait(false);
    }
    
    public async Task SetAnimeStatusAsync(int animeId, AnimeStatus status)
    {
        await UpdateMediaListEntry(animeId, status: status).ConfigureAwait(false);
    }

    public async Task SetAnimeScoreAsync(int animeId, int score)
    {
        await UpdateMediaListEntry(animeId, score: score).ConfigureAwait(false);
    }

    public async Task SetEpisodesWatchedAsync(int animeId, int episodes)
    {
        await UpdateMediaListEntry(animeId, progress: episodes).ConfigureAwait(false);
    }
    
    public async Task<AnimeDetails?> FetchAnimeDetailsAsync(int animeId, params AnimeField[] fields)
    {
        List<string> fragments = ["id"];
        fragments.AddRange(GetGraphQlFragments(fields.ToHashSet()));

        string query = $@"
            query ($id: Int) {{
                Media(id: $id, type: {Type}) {{
                    {string.Join("\n", fragments)}
                }}
            }}";

        GraphQLRequest request = new()
        {
            Query = query,
            Variables = new Dictionary<string, object> { { "id", animeId } }
        };

        var response = await TrySendQueryAsync<Anilist_MediaResponse>(request, $"FetchAnimeDetailsAsync {animeId} {string.Join(", ", fields)}").ConfigureAwait(false);
        if (response?.Data?.Media == null) return null;
            
        return ConvertAnilistToUnified(response.Data.Media, response.Data.Media.MediaListEntry);
    }

    public async Task<List<AnimeDetails>> SearchAnimeAsync(string query)
    {
        string innerQuery = string.Join("\n", GetGraphQlFragments([..AnimeService.MalNodeFieldTypes]));

        GraphQLRequest request = new()
        {
            Query = $@"
                query ($search: String) {{
                    Page(page: 1, perPage: 20) {{
                        media(search: $search, type: {Type}) {{
                            {innerQuery}
                        }}
                    }}
                }}",
            Variables =  new Dictionary<string, object> { ["search"] = query }
        };

        var response = await TrySendQueryAsync<Anilist_PageResponse>(request, "SearchAnimeAsync").ConfigureAwait(false);
        return response?.Data?.Page?.Media?.Select(m => ConvertAnilistToUnified(m, m.MediaListEntry)).ToList() ?? [];
    }

    public async Task<int?> GetIdFromMalIdAsync(int malId)
    {
        GraphQLRequest request = new()
        {
            Query = $"query ($idMal: Int) {{ Media(idMal: $idMal, type: {Type}) {{ id }} }}",
            Variables = new { idMal = malId }
        };

        var response = await TrySendQueryAsync<Anilist_MediaResponse>(request, $"GetIdFromMalIdAsync {malId}").ConfigureAwait(false);
        return response?.Data?.Media?.Id;
    }

    public async Task<List<RankingEntry>> GetTopAnimeAsync(RankingCategory category)
    {
        (string sort, string? status, string? country) ranking = IsManga ? category switch
        {
            RankingCategory.Airing => ("SCORE_DESC", null, "JP"),
            RankingCategory.Upcoming => ("SCORE_DESC", null, "KR"),
            RankingCategory.ByPopularity => ("POPULARITY_DESC", null, null),
            _ => ("SCORE_DESC", null, null)
        } : category switch
        {
            RankingCategory.Airing => ("POPULARITY_DESC", "RELEASING", null),
            RankingCategory.Upcoming => ("POPULARITY_DESC", "NOT_YET_RELEASED", null),
            RankingCategory.ByPopularity => ("POPULARITY_DESC", null, null),
            _ => ("SCORE_DESC", null, null)
        };
        (string sort, string? status, string? country) = ranking;

        HashSet<AnimeField> fields =
        [
            ..AnimeService.MalNodeFieldTypes,
            AnimeField.Videos
        ];
        var fragments = GetGraphQlFragments(fields);

        Dictionary<string, object> variables = new()
        {
            ["sort"] = sort,
            ["perPage"] = 100
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
        if (country != null)
        {
            variables["country"] = country;
            queryVariables += ", $country: CountryCode";
        }
        queryVariables += ")";
        
        string mediaArgs = $"type: {Type}, sort: $sort";
        if (status != null)
        {
            mediaArgs += ", status: $status";
        }
        if (country != null)
        {
            mediaArgs += ", countryOfOrigin: $country";
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

        var response = await TrySendQueryAsync<Anilist_PageResponse>(
            request, $"GetTopAnimeAsync {category}").ConfigureAwait(false);
        return response?.Data?.Page?.Media?
            .Select(m => new RankingEntry { Details = ConvertAnilistToUnified(m, m.MediaListEntry) })
            .ToList() ?? [];
    }

    #endregion

    private async Task UpdateMediaListEntry(int mediaId, AnimeStatus? status = null, int? score = null, int? progress = null)
    {
        if (!IsLoggedIn) return;

        Dictionary<string, object> variables = new()
        {
            ["mediaId"] = mediaId
        };

        if (status.HasValue)
            variables["status"] = ConvertToAnilistStatus(status.Value);
        if (score.HasValue)
            variables["scoreRaw"] = score.Value * 10;
        if (progress.HasValue)
            variables["progress"] = progress.Value;

        GraphQLRequest request = new()
        {
            Query = @"
                mutation ($mediaId: Int, $status: MediaListStatus, $scoreRaw: Int, $progress: Int) {
                    SaveMediaListEntry(mediaId: $mediaId, status: $status, scoreRaw: $scoreRaw, progress: $progress) {
                        id
                    }
                }",
            Variables = variables
        };

        await SendMutationAsync(request, "SaveMediaListEntry").ConfigureAwait(false);
    }

    private static string? GetTrailerUrl(Anilist_Trailer? trailer)
    {
        if (trailer == null) return null;
        
        return trailer.Site?.ToLowerInvariant() switch
        {
            "youtube" => $"https://www.youtube.com/watch?v={trailer.Id}",
            _ => null
        };
    }
    
    private AnimeDetails ConvertAnilistToUnified(Anilist_Anime anime, Anilist_MediaListStatus? userStatus)
    {
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
        
        List<RelatedAnime> relatedList = [];
        if (anime.Relations?.Edges != null)
        {
            foreach (Anilist_RelationEdge edge in anime.Relations.Edges)
            {
                if (edge.Node == null || (edge.Node.Type != null && edge.Node.Type != Type)) continue;
                
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
                        NumEpisodes = edge.Node.Episodes ?? edge.Node.Chapters,
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
            synopsis: StripHtml(anime.Description),
            alternativeTitles: new AlternativeTitles
            {
                English = anime.Title?.English,
                Japanese = anime.Title?.Native,
                Synonyms = null 
            },
            userStatus: userAnimeStatus,
            numEpisodes: anime.Episodes ?? anime.Chapters ?? 0,
            popularity: anime.Popularity,
            studios: anime.Studios?.Nodes?.Select(s => s.Name).ToArray()
                     ?? anime.Staff?.Nodes?.Select(s => s.Name?.Full ?? "").ToArray() ?? [],
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
    
    private static string? StripHtml(string? html) => html == null ? null :
        WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(html, @"<br\s*/?>\s*", "\n", RegexOptions.IgnoreCase), "<[^>]+>", "")).Trim();

    private static RelatedAnime.RelationType ConvertRelationType(string? type)
    {
        if (string.IsNullOrEmpty(type)) return RelatedAnime.RelationType.Other;
        return type switch
        {
            "PREQUEL" => RelatedAnime.RelationType.Prequel,
            "SEQUEL"  => RelatedAnime.RelationType.Sequel,
            "SUMMARY" =>  RelatedAnime.RelationType.Summary,
            "PARENT"  => RelatedAnime.RelationType.FullStory,
            _         => RelatedAnime.RelationType.Other
        };
    }
    
    private IEnumerable<string> GetGraphQlFragments(HashSet<AnimeField> fields)
    {
        if (fields.Count > 0)
        {
            yield return "id";
            yield return "idMal";
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
                    yield return "coverImage { large medium }";
                    yield return "bannerImage";
                    break;
                
                case AnimeField.Status:
                    yield return "status";
                    break;

                case AnimeField.Synopsis:
                    yield return "description";
                    break;

                case AnimeField.Episodes:
                    yield return IsManga ? "chapters" : "episodes";
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

                case AnimeField.Studios when IsManga:
                    yield return "staff(perPage: 2, sort: RELEVANCE) { nodes { name { full } } }";
                    break;

                case AnimeField.Studios:
                    yield return @"studios(isMain: true) {
                                nodes { id name }
                            }";
                    break;

                case AnimeField.Videos or AnimeField.TrailerUrl when IsManga:
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
                                        type
                                        episodes
                                        chapters
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
    
    private static string ConvertToAnilistStatus(AnimeStatus status)
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

    private static AnimeStatus ConvertFromAnilistStatus(string? status)
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

    private static MediaType ConvertStringToMediaType(string? mediaType)
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

    public void ClearRuntimeCache()
    {
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}