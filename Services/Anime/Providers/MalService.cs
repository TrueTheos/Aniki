using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aniki.Models.MAL.Components;
using Aniki.Services.Auth;
using Aniki.Services.Interfaces;
using Avalonia.Media.Imaging;

namespace Aniki.Services.Anime.Providers;

internal sealed class MalService : IAnimeProvider, IDisposable
{
    public ILoginProvider.ProviderType Provider => ILoginProvider.ProviderType.Mal;
    private bool _isLoggedIn;
    public bool IsLoggedIn => _isLoggedIn;

    private readonly JsonSerializerOptions _jso = new() { PropertyNameCaseInsensitive = true };
    private HttpClient _client = new();

    private Dictionary<int, AnimeDetails>? _userAnimeDict;
    private Task? _userListLoadTask;
    private readonly Lock _userListLock = new();
    private UserData? _cachedUserData;
    private readonly ConcurrentDictionary<string, List<AnimeDetails>> _searchCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Task<List<AnimeDetails>>> _searchInFlight = new(StringComparer.OrdinalIgnoreCase);

    private int _requestCounter;
    private int _waiting;
    private int _inFlight;
    
    private const int RATE_LIMIT = 5;
    private const int RATE_LIMIT_WINDOW_MS = 1000;
    private readonly SemaphoreSlim _rateLimitLock = new(1, 1);
    private readonly Queue<DateTime> _requestTimestamps = new();
    
    private readonly ISaveService _saveService;
    private readonly IJikanService _jikanService;

    private string _accessToken = "";
    private readonly string _allFields;
    private readonly string _listFields;
    private readonly string _searchFields;

    public MalService(ISaveService saveService, IJikanService jikanService)
    {
        _jikanService = jikanService;
        _allFields = string.Join(",", Enum.GetValues<AnimeField>()
                                          .Select(FieldToString)
                                          .Where(f => !string.IsNullOrEmpty(f)));
        _listFields = string.Join(",", AnimeService.MalNodeFieldTypes
                                          .Select(FieldToString)
                                          .Where(f => !string.IsNullOrEmpty(f)));
        // Search scoring needs alternative titles.
        _searchFields = _listFields + ",alternative_titles";
        _saveService = saveService;
    }

    public void ClearRuntimeCache()
    {
        _userAnimeDict = null;
        _userListLoadTask = null;
        _cachedUserData = null;
        _searchCache.Clear();
        _searchInFlight.Clear();
    }

    public Task InitAsync(string? accessToken)
    {
        _client.Dispose();
        _client = new();
        ClearRuntimeCache();
        
        if (accessToken != null)
        {
            _accessToken = accessToken;
            _isLoggedIn = true;
            _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_accessToken}");
        }
        else
        {
            _accessToken = string.Empty;
            _isLoggedIn = false;
            _client.DefaultRequestHeaders.Add("X-MAL-Client-ID", "dc4a7501af14aec92b98f719b666c37c");
        }

        return Task.CompletedTask;
    }
    
    private async Task<HttpResponseMessage> GetAsync(string url, string message)
    {
        int id = Interlocked.Increment(ref _requestCounter);
        int waiting = Interlocked.Increment(ref _waiting);
        Log(id, $"QUEUE  waiting={waiting} in-flight={Volatile.Read(ref _inFlight)} | {message}");

        Stopwatch total = Stopwatch.StartNew();
        await _rateLimitLock.WaitAsync().ConfigureAwait(false);
        Interlocked.Decrement(ref _waiting);

        try
        {
            DateTime now = DateTime.UtcNow;

            while (_requestTimestamps.Count > 0 &&
                   (now - _requestTimestamps.Peek()).TotalMilliseconds >= RATE_LIMIT_WINDOW_MS)
            {
                _requestTimestamps.Dequeue();
            }

            if (_requestTimestamps.Count >= RATE_LIMIT)
            {
                DateTime oldestRequest = _requestTimestamps.Peek();
                double timePassed = (now - oldestRequest).TotalMilliseconds;
                int timeToWait = (int)(RATE_LIMIT_WINDOW_MS - timePassed) + 20;

                if (timeToWait > 0)
                {
                    Log(id, $"WAIT   rate-limit {timeToWait}ms ({_requestTimestamps.Count}/{RATE_LIMIT} in window) | {message}");
                    await Task.Delay(timeToWait).ConfigureAwait(false);
                }

                while (_requestTimestamps.Count > 0 &&
                       (DateTime.UtcNow - _requestTimestamps.Peek()).TotalMilliseconds >= RATE_LIMIT_WINDOW_MS)
                {
                    _requestTimestamps.Dequeue();
                }
            }

            _requestTimestamps.Enqueue(DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            Log(id, $"FAIL   rate-limit gate: {ex.Message} | {message}");
        }
        finally
        {
            _rateLimitLock.Release();
        }

        int inFlight = Interlocked.Increment(ref _inFlight);
        Log(id, $"START  in-flight={inFlight} waiting={Volatile.Read(ref _waiting)} | {message}");

        Stopwatch http = Stopwatch.StartNew();
        try
        {
            HttpResponseMessage result = await _client.GetAsync(url).ConfigureAwait(false);
            Log(id, $"DONE   http={http.ElapsedMilliseconds}ms total={total.ElapsedMilliseconds}ms status={(int)result.StatusCode} in-flight={Volatile.Read(ref _inFlight) - 1} | {message}");
            return result;
        }
        catch (Exception ex)
        {
            Log(id, $"FAIL   http={http.ElapsedMilliseconds}ms total={total.ElapsedMilliseconds}ms {ex.GetType().Name}: {ex.Message} | {message}");
            throw;
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
        }
    }

    private static void Log(int id, string message) =>
        Console.WriteLine($"[MAL #{id}] {message}");

    private async Task<T?> GetAndDeserializeAsync<T>(string url, string message) where T : class
    {
        url = url.Replace("%20", "+", StringComparison.InvariantCulture).Replace(" ", "+", StringComparison.InvariantCulture);
        HttpResponseMessage response = await GetAsync(url, message).ConfigureAwait(false);
        
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"API returned status code: {response.StatusCode} {url} {message}");
        }

        string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(responseBody, _jso);
    }

    #region Require Login

    public async Task<UserData> GetUserDataAsync()
    {
        if (!IsLoggedIn) return new();
        if (_cachedUserData != null) return _cachedUserData;
        
        MAL_UserData? userData = await GetAndDeserializeAsync<MAL_UserData>("https://api.myanimelist.net/v2/users/@me", "GetUserDataAsync").ConfigureAwait(false);
        
        if(userData == null) throw new InvalidOperationException("Failed to deserialize user data");
        _cachedUserData = new UserData
        {
            Id = userData.Id,
            Name = userData.Name,
            Picture = userData.Picture
        };
        return _cachedUserData;
    }
    
    private static AnimeStatus ConvertFromMalStatus(AnimeStatusApi malStatus)
    {
        return malStatus switch
        {
            AnimeStatusApi.watching => AnimeStatus.Watching,
            AnimeStatusApi.completed => AnimeStatus.Completed,
            AnimeStatusApi.on_hold => AnimeStatus.OnHold,
            AnimeStatusApi.dropped => AnimeStatus.Dropped,
            AnimeStatusApi.plan_to_watch => AnimeStatus.PlanToWatch,
            _ => AnimeStatus.None
        };
    }
    
    private static UserAnimeStatus? ConvertMalListStatus(MAL_MyListStatus? malStatus)
    {
        if (malStatus == null) return null;
        
        return new UserAnimeStatus
        {
            Status = ConvertFromMalStatus(malStatus.Status),
            Score = malStatus.Score,
            EpisodesWatched = malStatus.NumEpisodesWatched
        };
    }

    public async Task<List<AnimeDetails>> GetUserAnimeListAsync(AnimeStatus status = AnimeStatus.None)
    {
        if (!IsLoggedIn) return [];

        if (_userAnimeDict == null)
        {
            Task loadTask;
            lock (_userListLock)
            {
                _userListLoadTask ??= LoadUserAnimeListAsync();
                loadTask = _userListLoadTask;
            }

            try
            {
                await loadTask.ConfigureAwait(false);
            }
            catch
            {
                lock (_userListLock) { _userListLoadTask = null; }
                throw;
            }
        }

        return status == AnimeStatus.None
            ? _userAnimeDict!.Values.ToList()
            : _userAnimeDict!.Values.Where(a => a.UserStatus?.Status == status).ToList();
    }

    private async Task LoadUserAnimeListAsync()
    {
        try
        {
            string? nextPageUrl =
                $"https://api.myanimelist.net/v2/users/@me/animelist?fields={_listFields}&limit=1000&nsfw=true";

            List<AnimeDetails> fetchedList = [];
            while (nextPageUrl != null)
            {
                MAL_UserAnimeListResponse? response =
                    await GetAndDeserializeAsync<MAL_UserAnimeListResponse>(nextPageUrl, "GetUserAnimeList")
                        .ConfigureAwait(false);
                if (response?.Data != null)
                {
                    fetchedList.AddRange(response.Data.Select(x => ConvertMalToUnified(x.Node)));
                }

                nextPageUrl = response?.Paging?.Next;
            }

            _userAnimeDict = fetchedList.ToDictionary(a => a.Id);
        }
        catch (Exception ex)
        {
            throw new NotSupportedException($"Error loading anime list: {ex.Message}", ex);
        }
    }

    public async Task SetAnimeStatusAsync(int animeId, AnimeStatus status)
    {
        AnimeStatusApi apiStatus = ConvertToMalStatus(status);
        if (apiStatus == AnimeStatusApi.none)
        {
            await RemoveFromUserListAsync(animeId).ConfigureAwait(false);
            return;
        }

        bool success = await SetMyListStatusField(
            animeId, UserAnimeStatus.UserAnimeStatusField.Status, StatusEnum.ApiToString(apiStatus)).ConfigureAwait(false);
        if (success) PatchUserListStatus(animeId, s => s.Status = status);
    }

    public async Task SetAnimeScoreAsync(int animeId, int score)
    {
        bool success = await SetMyListStatusField(animeId, UserAnimeStatus.UserAnimeStatusField.Score, score.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
        if (success) PatchUserListStatus(animeId, s => s.Score = score);
    }

    public async Task SetEpisodesWatchedAsync(int animeId, int episodes)
    {
        bool success = await SetMyListStatusField(animeId, UserAnimeStatus.UserAnimeStatusField.EpisodesWatched, episodes.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
        if (success) PatchUserListStatus(animeId, s => s.EpisodesWatched = episodes);
    }

    private void PatchUserListStatus(int animeId, Action<UserAnimeStatus> patch)
    {
        if (_userAnimeDict == null || !_userAnimeDict.TryGetValue(animeId, out AnimeDetails? anime))
            return;

        anime.UserStatus ??= new UserAnimeStatus();
        patch(anime.UserStatus);
    }
    
    private async Task<bool> SetMyListStatusField(int animeId, UserAnimeStatus.UserAnimeStatusField field, string value)
    {
        if (!IsLoggedIn)
        {
            throw new InvalidOperationException("Not logged in to MyAnimeList");
        }

        string fieldName = field switch
        {
            UserAnimeStatus.UserAnimeStatusField.Status => "status",
            UserAnimeStatus.UserAnimeStatusField.Score => "score",
            UserAnimeStatus.UserAnimeStatusField.EpisodesWatched => "num_watched_episodes",
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null)
        };
        
        Dictionary<string, string> formData = new() { [fieldName] = value };

        int id = Interlocked.Increment(ref _requestCounter);
        Log(id, $"START  PUT my_list_status {fieldName}={value} anime={animeId}");
        Stopwatch http = Stopwatch.StartNew();

#pragma warning disable CA2000
        FormUrlEncodedContent content = new(formData);
#pragma warning restore CA2000
        HttpResponseMessage response = await _client.PutAsync(
            $"https://api.myanimelist.net/v2/anime/{animeId}/my_list_status", content).ConfigureAwait(false);

        Log(id, $"DONE   PUT my_list_status http={http.ElapsedMilliseconds}ms status={(int)response.StatusCode} | {fieldName}={value} anime={animeId}");

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new NotSupportedException($"Failed to update anime: {response.StatusCode} {body}");
        }

        if (_userAnimeDict != null && !_userAnimeDict.ContainsKey(animeId))
        {
            AnimeDetails? details = await FetchAnimeDetailsAsync(animeId, AnimeService.MalNodeFieldTypes).ConfigureAwait(false);
            if (details != null) _userAnimeDict[animeId] = details;
        }
        
        return true;
    }
    
    public async Task RemoveFromUserListAsync(int animeId)
    {
        if(!IsLoggedIn) return;
        
        HttpResponseMessage response = await _client.DeleteAsync($"https://api.myanimelist.net/v2/anime/{animeId}/my_list_status").ConfigureAwait(false);

        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            throw new NotSupportedException($"Failed to remove anime from list: {response.StatusCode}");
        }
        else
        {
            _userAnimeDict?.Remove(animeId);
        }
    }

    #endregion
    
    public async Task<AnimeDetails?> FetchAnimeDetailsAsync(int id, params AnimeField[] fields)
    {
        StringBuilder urlFields = new();
        Bitmap? picture = null;
        foreach (AnimeField field in fields)
        {
            if (field is AnimeField.MainPicture && _saveService.TryGetAnimeImage(id, out picture)) {}
            else if (field is not (AnimeField.Picture or AnimeField.TrailerUrl or AnimeField.Id))
            {
                urlFields.Append(CultureInfo.InvariantCulture, $"{FieldToString(field)},");
            }
        }
        
        string url = $"https://api.myanimelist.net/v2/anime/{id}?fields={urlFields}&nsfw=true";
        
        MAL_Anime? animeResponse = await GetAndDeserializeAsync<MAL_Anime>(url, $"FetchFields {id} {urlFields}").ConfigureAwait(false);

        if (animeResponse != null)
        {
            animeResponse.Id = id;
            
            if(fields.Contains(AnimeField.TrailerUrl)) animeResponse.TrailerUrl = await _jikanService.GetAnimeTrailerUrlAsync(animeResponse.Id).ConfigureAwait(false);
            if (fields.Contains(AnimeField.Picture))
            {
                if (picture != null )
                {
                    animeResponse.Picture = picture;
                }
            }
            else
            {
                animeResponse.Picture = await LoadAnimeImageAsync(id, animeResponse.MainPicture?.Large).ConfigureAwait(false);
            }

            return ConvertMalToUnified(animeResponse);
        }

        return null;
    }

    private string FieldToString(AnimeField field)
    {
        return field switch
        {
            AnimeField.Title => "title",
            AnimeField.MainPicture => "main_picture",
            AnimeField.Status => "status",
            AnimeField.Synopsis => "synopsis",
            AnimeField.AlterTitles => "alternative_titles",
            AnimeField.MyListStatus => "my_list_status",
            AnimeField.Episodes => "num_episodes",
            AnimeField.Popularity => "popularity",
            AnimeField.Picture => "",
            AnimeField.Studios => "studios",
            AnimeField.StartDate => "start_date",
            AnimeField.Mean => "mean",
            AnimeField.MediaType => "media_type",
            AnimeField.Genres => "genres",
            AnimeField.RelatedAnime =>
                "related_anime{id,title,num_episodes,media_type,synopsis,status,alternative_titles}",
            AnimeField.Videos     => "videos",
            AnimeField.NumFav     => "num_favorites",
            AnimeField.Stats      => "statistics",
            AnimeField.TrailerUrl => "",
            AnimeField.Id         => "",
            _                     => ""
        };
    }

    public async Task<Bitmap?> LoadAnimeImageAsync(int id, string? imageUrl)
    {
        if (_saveService.TryGetAnimeImage(id, out Bitmap? bitmap))
        {
            return bitmap;
        }
        else
        {
            if (imageUrl != null)
            {
                Bitmap? downloadedImage = await GetAnimeImage(imageUrl).ConfigureAwait(false);
                if (downloadedImage != null)
                {
                    _saveService.SaveImage(id, downloadedImage);
                    return downloadedImage;
                }
            }
            
        }

        return null;
    }

    public async Task<Bitmap?> GetAnimeImage(string animePictureData)
    {
        try
        {
            return await DownloadImageAsync(animePictureData).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting anime picture: {ex.Message}");
            return null;
        }
    }

    private async Task<Bitmap?> DownloadImageAsync(string url)
    {
        try
        {
            byte[] imageData = await _client.GetByteArrayAsync(url).ConfigureAwait(false);
            using MemoryStream ms = new(imageData);
            return new Bitmap(ms);
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<AnimeDetails>> SearchAnimeAsync(string query)
    {
        string cacheKey = query.Trim();
        if (_searchCache.TryGetValue(cacheKey, out List<AnimeDetails>? cached))
            return cached.ToList();

        Task<List<AnimeDetails>> task = _searchInFlight.GetOrAdd(cacheKey, SearchAnimeCoreAsync);
        try
        {
            List<AnimeDetails> results = await task.ConfigureAwait(false);
            _searchCache[cacheKey] = results;
            return results.ToList();
        }
        finally
        {
            _searchInFlight.TryRemove(cacheKey, out _);
        }
    }

    private async Task<List<AnimeDetails>> SearchAnimeCoreAsync(string query)
    {
        string url =
            $"https://api.myanimelist.net/v2/anime?q={Uri.EscapeDataString(query)}&limit=20&fields={_searchFields}&nsfw=true";

        MAL_AnimeSearchListResponse? responseData =
            await GetAndDeserializeAsync<MAL_AnimeSearchListResponse>(url, $"SearchAnimeOrdered {query}")
                .ConfigureAwait(false);

        return responseData?.Data?
                   .OrderBy(x => Math.Abs(x.Node.Title?.Length - query.Length ?? 0))
                   .Select(x => new { Entry = x, Score = CalculateSearchScore(x.Node, query) })
                   .OrderByDescending(x => x.Score)
                   .Select(x => ConvertMalToUnified(x.Entry.Node))
                   .ToList()
               ?? [];
    }

    private static int CalculateSearchScore(MAL_Anime anime, string query)
    {
        if (DoesTitleMatch(anime, query, out int s))
        {
            return s;
        }

        int bestScore = 0;
        bestScore = Math.Max(bestScore, ScoreTitle(anime.Title, query));

        if (anime.AlternativeTitles == null) 
            return bestScore;
        
        bestScore = Math.Max(bestScore, ScoreTitle(anime.AlternativeTitles.En, query));
        bestScore = Math.Max(bestScore, ScoreTitle(anime.AlternativeTitles.Ja, query));

        if (anime.AlternativeTitles.Synonyms != null)
        {
            bestScore = anime.AlternativeTitles.Synonyms.Select(synonym => ScoreTitle(synonym, query)).Prepend(bestScore).Max();
        }
        
        return bestScore;
    }
    
    private static int ScoreTitle(string? title, string query)
    {
        if (string.IsNullOrWhiteSpace(title))
            return 0;

        int score = FuzzySharp.Fuzz.TokenSortRatio(title, query);

        if (title.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            score += 50;
        }

        return score;
    }

    /// so because MAL is fucking stupid and when u search for `One Piece` the top 3 results are:
    /// One Piece Film: Z
    /// One Piece Film: Gold
    /// One Piece
    /// and watchout cuz One Piece Film: Gold has japanese title ONE PIECE. So basically this returns as a perfect match.
    /// Thats why I need to give lower scores 
    
    private static bool DoesTitleMatch(MAL_Anime malAnime, string query, out int score)
    {
        string normalizedQuery = NormalizeTitleToLower(query);
        string normalizedTitle = NormalizeTitleToLower(malAnime.Title);

        if (normalizedTitle == normalizedQuery)
        {
            score = 1000;
            return true;
        }
        if (malAnime.AlternativeTitles == null)
        {
            score = 0;
            return false;
        }
        
        string normalizedEn = NormalizeTitleToLower(malAnime.AlternativeTitles.En);
        string normalizedJp = NormalizeTitleToLower(malAnime.AlternativeTitles.Ja);

        if (normalizedJp == normalizedQuery)
        {
            score = 900;
            return true;
        }
        if (normalizedEn == normalizedQuery)
        {
            score = 1000;
            return true;
        }

        if (malAnime.AlternativeTitles.Synonyms?.Any(x => NormalizeTitleToLower(x) == normalizedQuery) == true)
        {
            score = 800;
            return true;
        }
        score = 0;
        return false;
    }

    private static string NormalizeTitleToLower(string? title)
    {
        if (string.IsNullOrEmpty(title)) return string.Empty;
    
        string normalized = title.Replace("-", "", StringComparison.InvariantCulture).Replace("_", "", StringComparison.InvariantCulture).Replace(":", "", StringComparison.InvariantCulture).Trim();
        normalized = Regex.Replace(normalized, @"\s+", " ");
        return normalized.ToLowerInvariant();
    }
    
    private static AnimeStatusApi ConvertToMalStatus(AnimeStatus status)
    {
        return status switch
        {
            AnimeStatus.Watching => AnimeStatusApi.watching,
            AnimeStatus.Completed => AnimeStatusApi.completed,
            AnimeStatus.OnHold => AnimeStatusApi.on_hold,
            AnimeStatus.Dropped => AnimeStatusApi.dropped,
            AnimeStatus.PlanToWatch => AnimeStatusApi.plan_to_watch,
            _ => AnimeStatusApi.none
        };
    }
    
    
    private static RelatedAnime.RelationType ConvertRelationType(string? type)
    {
        if (string.IsNullOrEmpty(type)) return RelatedAnime.RelationType.Other;
        return type switch
        {
            "prequel"    => RelatedAnime.RelationType.Prequel,
            "sequel"     => RelatedAnime.RelationType.Sequel,
            "summary"    =>  RelatedAnime.RelationType.Summary,
            "full_story" => RelatedAnime.RelationType.FullStory,
            _            => RelatedAnime.RelationType.Other
        };
    }
    
    private static AnimeDetails ConvertMalToUnified(MAL_Anime mal)
    {
        return new AnimeDetails(
            id: mal.Id,
            title: mal.Title,
            mainPicture: mal.MainPicture != null
                ? new AnimePicture
                {
                    Medium = mal.MainPicture.Medium,
                    Large = mal.MainPicture.Large
                } : null,
            status: mal.Status,
            synopsis: mal.Synopsis,
            alternativeTitles: mal.AlternativeTitles != null
                ? new AlternativeTitles
                {
                    Synonyms = mal.AlternativeTitles.Synonyms,
                    English = mal.AlternativeTitles.En,
                    Japanese = mal.AlternativeTitles.Ja
                } : null,
            userStatus: ConvertMalListStatus(mal.MyListStatus),
            numEpisodes: mal.NumEpisodes,
            popularity: mal.Popularity,
            picture: mal.Picture,
            studios: mal.Studios?.Select(s => s.Name).ToArray(),
            startDate: mal.StartDate,
            mean: mal.Mean,
            genres: mal.Genres?.Select(g => g.Name).ToArray(),
            trailerUrl: mal.TrailerUrl,
            numFavorites: mal.NumFavorites,
            videos: mal.Videos,
            statistics: mal.Statistics?.ToAnimeStatistics() ?? new AnimeStatistics(),
            mediaType: mal.MediaType ?? MediaType.Unknown,
            relatedAnime: mal.RelatedAnime?
                .Select(r => new RelatedAnime
                {
                    Details = r.Node != null ? ConvertMalToUnified(r.Node) : null,
                    Relation = ConvertRelationType(r.RelationType)
                })
                .ToArray() ?? []);
    }

    public void Dispose()
    {
        _client.Dispose();
        _rateLimitLock.Dispose();
    }
}