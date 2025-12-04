using System.Diagnostics;
using System.Text;
using Avalonia.Media.Imaging;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aniki.Services.Anime;
using Aniki.Services.Auth;
using Aniki.Services.Interfaces;

namespace Aniki.Services;

public class MalService : IAnimeProvider
{
    public ILoginProvider.ProviderType Provider => ILoginProvider.ProviderType.MAL;
    private bool _isLoggedIn;
    public bool IsLoggedIn => _isLoggedIn;

    public enum AnimeRankingCategory { AIRING, UPCOMING, ALLTIME, BYPOPULARITY }
    
    private readonly JsonSerializerOptions _jso = new() { PropertyNameCaseInsensitive = true };
    private HttpClient _client = new();
    
    private List<AnimeDetails>? _userAnimeList;

    private readonly Stopwatch _sw = new();
    private int _requestCounter;
    
    private const int RateLimit = 100;
    private const int RateLimitWindowMs = 1000;
    private readonly SemaphoreSlim _rateLimitLock = new(1, 1);
    private readonly Queue<DateTime> _requestTimestamps = new();
    
    private readonly ISaveService _saveService;
    private readonly ITokenService _tokenService;

    private string _accessToken = "";

    private string _allFields = "";

    public MalService(ISaveService saveService, ITokenService tokenService)
    {
        StringBuilder urlFields = new();
        foreach (AnimeField field in Enum.GetValues<AnimeField>())
        {
            urlFields.Append($"{FieldToString(field)},");
        }

        _allFields = urlFields.ToString();
        
        _tokenService = tokenService;
        _saveService = saveService;
    }


    public void Init(string? accessToken)
    {
        _client = new();
        
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
    }
    
    private async Task<HttpResponseMessage> GetAsync(string url, string message)
    {
        await _rateLimitLock.WaitAsync();

        try
        {
            DateTime now = DateTime.UtcNow;

            while (_requestTimestamps.Count > 0 &&
                   (now - _requestTimestamps.Peek()).TotalMilliseconds >= RateLimitWindowMs)
            {
                _requestTimestamps.Dequeue();
            }

            if (_requestTimestamps.Count >= RateLimit)
            {
                DateTime oldestRequest = _requestTimestamps.Peek();
                double timePassed = (now - oldestRequest).TotalMilliseconds;
                int timeToWait = (int)(RateLimitWindowMs - timePassed);

                if (timeToWait > 0)
                {
                    await Task.Delay(timeToWait + 20);
                }

                while (_requestTimestamps.Count > 0 &&
                       (DateTime.UtcNow - _requestTimestamps.Peek()).TotalMilliseconds >= RateLimitWindowMs)
                {
                    _requestTimestamps.Dequeue();
                }
            }

            _requestTimestamps.Enqueue(DateTime.UtcNow);
        }
        catch(Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
        finally
        {
            _rateLimitLock.Release();
        }

    #if DEBUG
        _sw.Restart();
    #endif
        
        HttpResponseMessage result = await _client.GetAsync(url);

    #if DEBUG
        _sw.Stop();
        Console.WriteLine($"{_requestCounter}: {message} took: {_sw.ElapsedMilliseconds}ms");
    #endif
        _requestCounter++;
        return result;
    }

    private async Task<T?> GetAndDeserializeAsync<T>(string url, string message) where T : class
    {
        HttpResponseMessage response = await GetAsync(url, message);
        
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"API returned status code: {response.StatusCode} {url} {message}");
        }

        string responseBody = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(responseBody, _jso);
    }

    #region Require Login

    public async Task<UserData> GetUserDataAsync()
    {
        if (!IsLoggedIn) return new();
        
        MAL_UserData? userData = await GetAndDeserializeAsync<MAL_UserData>("https://api.myanimelist.net/v2/users/@me", "GetUserDataAsync");
        
        if(userData == null) throw new InvalidOperationException("Failed to deserialize user data");
        return new UserData
        {
            Id = userData.Id,
            Name = userData.Name,
            Picture = userData.Picture
        };
    }
    
    private AnimeStatus ConvertFromMalStatus(AnimeStatusApi malStatus)
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
    
    private UserAnimeStatus? ConvertMalListStatus(MalMyListStatus? malStatus)
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
        if (!IsLoggedIn) return new();
        
        if (_userAnimeList != null)
        {
            return status == AnimeStatus.None 
                ? _userAnimeList 
                : _userAnimeList.Where(a => a.UserStatus?.Status == status).ToList();
        }

        try
        {
            List<AnimeDetails> animeList = new();
            Console.WriteLine("2 GetUserAnimeListAsync");
            string baseUrl = $"https://api.myanimelist.net/v2/users/@me/animelist?fields={_allFields}&limit=1000&nsfw=true";
            
            if (status != AnimeStatus.None)
            {
                baseUrl += $"&status={status}";
            }

            string? nextPageUrl = baseUrl;
            
            while (nextPageUrl != null)
            {
                MalUserAnimeListResponse? response = await GetAndDeserializeAsync<MalUserAnimeListResponse>(nextPageUrl, "GetUserAnimeList");
                if (response?.Data != null)
                {
                    animeList.AddRange( response.Data.Select(x => ConvertMalToUnified(x.Node)).ToList());
                }
                nextPageUrl = response?.Paging?.Next;
            }

            if (status == AnimeStatus.None)
            {
                _userAnimeList = animeList;
            }
            return animeList;
        }
        catch (Exception ex)
        {
            throw new($"Error loading anime list: {ex.Message}", ex);
        }
    }
    
    public Task SetAnimeStatusAsync(int animeId, AnimeStatus status) 
        => SetMyListStatusField(animeId, UserAnimeStatus.UserAnimeStatusField.Status, ConvertToMalStatus(status).ToString());

    public Task SetAnimeScoreAsync(int animeId, int score) 
        => SetMyListStatusField(animeId, UserAnimeStatus.UserAnimeStatusField.Score, score.ToString());

    public Task SetEpisodesWatchedAsync(int animeId, int episodes) 
        => SetMyListStatusField(animeId, UserAnimeStatus.UserAnimeStatusField.EpisodesWatched, episodes.ToString());

    
    private async Task SetMyListStatusField(int  animeId, UserAnimeStatus.UserAnimeStatusField field, string value)
    {
        if (!IsLoggedIn)
        {
            return;
        }

        string fieldName = field switch
        {
            UserAnimeStatus.UserAnimeStatusField.Status => "status",
            UserAnimeStatus.UserAnimeStatusField.Score => "score",
            UserAnimeStatus.UserAnimeStatusField.EpisodesWatched => "num_watched_episodes",
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null)
        };
        
        Dictionary<string, string> formData = new() { [fieldName] = value };
        
        FormUrlEncodedContent content = new(formData);
        HttpResponseMessage response = await _client.PutAsync($"https://api.myanimelist.net/v2/anime/{animeId}/my_list_status", content);

        if (!response.IsSuccessStatusCode)
        {
            throw new($"Failed to update anime: {response.StatusCode}");
        }
    }
    
    public async Task RemoveFromUserListAsync(int animeId)
    {
        if(!IsLoggedIn) return;
        
        HttpResponseMessage response = await _client.DeleteAsync($"https://api.myanimelist.net/v2/anime/{animeId}/my_list_status");

        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            throw new($"Failed to remove anime from list: {response.StatusCode}");
        }

        _userAnimeList = null;
    }

    #endregion
    
    public async Task<AnimeDetails?> FetchAnimeDetailsAsync(int id, params AnimeField[] fields)
    {
        StringBuilder urlFields = new();
        Bitmap? picture = null;
        foreach (AnimeField field in fields)
        {
            if (field is AnimeField.MAIN_PICTURE && _saveService.TryGetAnimeImage(id, out picture)) {}
            else if (field is not (AnimeField.PICTURE or AnimeField.TRAILER_URL or AnimeField.ID))
            {
                urlFields.Append($"{FieldToString(field)},");
            }
        }
        
        string url = $"https://api.myanimelist.net/v2/anime/{id}?fields={urlFields}&nsfw=true";
        
        MalAnimeDetails? animeResponse = await GetAndDeserializeAsync<MalAnimeDetails>(url, $"FetchFields {id} {urlFields}");

        if (animeResponse != null)
        {
            animeResponse.Id = id;
            
            if(fields.Contains(AnimeField.TRAILER_URL)) animeResponse.TrailerUrl = await GetAnimeTrailerUrlJikan(animeResponse.Id);
            if (fields.Contains(AnimeField.PICTURE))
            {
                if (picture != null )
                {
                    animeResponse.Picture = picture;
                }
            }
            else
            {
                animeResponse.Picture = await LoadAnimeImageAsync(id, animeResponse.MainPicture?.Large);
            }

            return ConvertMalToUnified(animeResponse);
        }

        return null;
    }

    private string FieldToString(AnimeField field)
    {
        return field switch
        {
            AnimeField.TITLE => "title",
            AnimeField.MAIN_PICTURE => "main_picture",
            AnimeField.STATUS => "status",
            AnimeField.SYNOPSIS => "synopsis",
            AnimeField.ALTER_TITLES => "alternative_titles",
            AnimeField.MY_LIST_STATUS => "my_list_status",
            AnimeField.EPISODES => "num_episodes",
            AnimeField.POPULARITY => "popularity",
            AnimeField.PICTURE => "",
            AnimeField.STUDIOS => "studios",
            AnimeField.START_DATE => "start_date",
            AnimeField.MEAN => "mean",
            AnimeField.GENRES => "genres",
            AnimeField.RELATED_ANIME =>
                "related_anime{id,title,num_episodes,media_type,synopsis,status,alternative_titles}",
            AnimeField.VIDEOS => "videos",
            AnimeField.NUM_FAV => "num_favorites",
            AnimeField.STATS => "statistics",
            AnimeField.TRAILER_URL => "",
            _ => ""
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
                Bitmap? downloadedImage = await GetAnimeImage(imageUrl);
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
            return await DownloadImageAsync(animePictureData);
        }
        catch (Exception ex)
        {
            Log.Information($"Error getting anime picture: {ex.Message}");
            return null;
        }
    }

    private async Task<Bitmap?> DownloadImageAsync(string url)
    {
        try
        {
            byte[] imageData = await _client.GetByteArrayAsync(url);
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
        string url = $"https://api.myanimelist.net/v2/anime?q={Uri.EscapeDataString(query)}&limit=20&fields={_allFields}&nsfw=true";

        MalAnimeSearchListResponse? responseData = await GetAndDeserializeAsync<MalAnimeSearchListResponse>(url, $"SearchAnimeOrdered {query}");

        List<MalSearchEntry> results = responseData?.Data?
            .Select(x => new { Entry = x, Score = CalculateSearchScore(x.Node, query) })
            .OrderByDescending(x => x.Score)
            .Select(x => x.Entry)
            .ToList() ?? new List<MalSearchEntry>();

        return results.Select(mal => ConvertMalToUnified(mal.Node)).ToList();
    }

    private int CalculateSearchScore(MalAnimeDetails anime, string query)
    {
        if (DoesTitleMatch(anime, query))
        {
            return 1000;
        }
        
        int score = FuzzySharp.Fuzz.TokenSortRatio(anime.Title, query);
        
        if (anime.Title != null && anime.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            score += 50;
        }

        return score;
    }

    private bool DoesTitleMatch(MalAnimeDetails malAnime, string query)
    {
        string normalizedQuery = NormalizeTitleToLower(query);
        string normalizedTitle = NormalizeTitleToLower(malAnime.Title);
        
        if (normalizedTitle == normalizedQuery) return true;
        if (malAnime.AlternativeTitles == null) return false;
        
        string normalizedEn = NormalizeTitleToLower(malAnime.AlternativeTitles.En);
        string normalizedJp = NormalizeTitleToLower(malAnime.AlternativeTitles.Ja);
        
        if (normalizedJp == normalizedQuery || normalizedEn == normalizedQuery) return true;
        
        return malAnime.AlternativeTitles.Synonyms?.Any(x => NormalizeTitleToLower(x) == normalizedQuery) == true;
    }

    private string NormalizeTitleToLower(string? title)
    {
        if (string.IsNullOrEmpty(title)) return string.Empty;
    
        string normalized = title.Replace("-", "").Replace("_", "").Replace(":", "").Trim();
        normalized = Regex.Replace(normalized, @"\s+", " ");
        return normalized.ToLower();
    }
    
    private MalService.AnimeRankingCategory ConvertToMalRankingCategory(RankingCategory category)
    {
        return category switch
        {
            RankingCategory.Airing => MalService.AnimeRankingCategory.AIRING,
            RankingCategory.Upcoming => MalService.AnimeRankingCategory.UPCOMING,
            RankingCategory.AllTime => MalService.AnimeRankingCategory.ALLTIME,
            RankingCategory.ByPopularity => MalService.AnimeRankingCategory.BYPOPULARITY,
            _ => MalService.AnimeRankingCategory.ALLTIME
        };
    }

    public async Task<List<RankingEntry>> GetTopAnimeAsync(RankingCategory category, int limit = 10)
    {
        string rankingType = ConvertToMalRankingCategory(category) switch
        {
            AnimeRankingCategory.AIRING => "airing",
            AnimeRankingCategory.UPCOMING => "upcoming",
            AnimeRankingCategory.ALLTIME => "all",
            AnimeRankingCategory.BYPOPULARITY => "bypopularity",
            _ => "all"
        };

        string url = $"https://api.myanimelist.net/v2/anime/ranking?ranking_type={rankingType}&limit={limit}&fields={_allFields}&nsfw=true";

        MAL_AnimeRankingResponse? response = await GetAndDeserializeAsync<MAL_AnimeRankingResponse>(url, "GetTopAnimeInCategory");
        
        if (response?.Data != null)
        {
            return response.Data.Select(mal => new RankingEntry
            {
                Details = ConvertMalToUnified(mal.Node),
            }).ToList();
        }

        return new List<RankingEntry>();
    }

    private AnimeStatusApi ConvertToMalStatus(AnimeStatus status)
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
    
    private async Task<string?> GetAnimeTrailerUrlJikan(int animeId)
    {
        try
        {
            string url = $"https://api.jikan.moe/v4/anime/{animeId}/videos";

            HttpResponseMessage response = await GetAsync(url, "GetAnimeTrailerUrlJikan");

            if (!response.IsSuccessStatusCode)
            {
                Log.Information($"Failed to get trailer from Jikan: {response.StatusCode}");
                return null;
            }

            string json = await response.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("data", out JsonElement dataElem) &&
                dataElem.TryGetProperty("promo", out JsonElement promoArray) &&
                promoArray.ValueKind == JsonValueKind.Array &&
                promoArray.GetArrayLength() > 0)
            {
                JsonElement firstPromo = promoArray[0];
                if (firstPromo.TryGetProperty("trailer", out JsonElement trailerElem))
                {
                    if (trailerElem.TryGetProperty("embed_url", out JsonElement urlElem))
                    {
                        return urlElem.GetString();
                    }
                    if (trailerElem.TryGetProperty("youtube_id", out JsonElement youtubeIdElem))
                    {
                        string youtubeId = youtubeIdElem.GetString() ?? "";
                        return string.IsNullOrEmpty(youtubeId) ? null : $"https://www.youtube.com/watch?v={youtubeId}";
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            Log.Information($"Error getting trailer from Jikan: {ex.Message}");
            return null;
        }
    }
    
    private RelatedAnime.RelationType ConvertRelationType(string? type)
    {
        if (string.IsNullOrEmpty(type)) return RelatedAnime.RelationType.OTHER;
        return type switch
        {
            "prequel" => RelatedAnime.RelationType.PREQUEL,
            "sequel" => RelatedAnime.RelationType.SEQUEL,
            _ => RelatedAnime.RelationType.OTHER
        };
    }
    
    private AnimeDetails ConvertMalToUnified(MalAnimeDetails mal)
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
            relatedAnime: mal.RelatedAnime?
                .Select(r => new Models.RelatedAnime
                {
                    Details = r.Node != null ? ConvertMalToUnified(r.Node) : null,
                    Relation = ConvertRelationType(r.RelationType)
                })
                .ToArray() ?? []);
    }
}