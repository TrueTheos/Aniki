using System.Diagnostics;
using System.Text;
using Avalonia.Media.Imaging;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aniki.Services.Interfaces;

namespace Aniki.Services;

public class MalService : IMalService
{
    public static bool IS_LOGGED_IN { get; private set; }

    private readonly GenericCacheService<int, MalAnimeDetails, AnimeField> _cache;
    
    public enum AnimeRankingCategory { AIRING, UPCOMING, ALLTIME, BYPOPULARITY }
    
    private readonly JsonSerializerOptions _jso = new() { PropertyNameCaseInsensitive = true };
    private HttpClient _client = new();
    
    private List<MalAnimeData>? _userAnimeList;

    private readonly Stopwatch _sw = new();
    private int _requestCounter;
    private readonly Queue<DateTime> _requestTimestamps = new();
    
    private readonly ISaveService _saveService;
    private readonly ITokenService _tokenService;

    private string _accessToken = "";
    
    public const string MAL_NODE_FIELDS = "id,title,num_episodes,list_status,pictures,status,genres,synopsis,main_picture,mean,popularity,my_list_status,start_date,studios";

    public static readonly AnimeField[] MAL_NODE_FIELD_TYPES = new[]
    {
        AnimeField.ID, AnimeField.MY_LIST_STATUS, AnimeField.STATUS, AnimeField.GENRES, AnimeField.SYNOPSIS, AnimeField.MAIN_PICTURE,
        AnimeField.MEAN, AnimeField.POPULARITY, AnimeField.START_DATE, AnimeField.STUDIOS, AnimeField.TITLE, AnimeField.EPISODES
    };

    public MalService(ISaveService saveService, ITokenService tokenService)
    {
        CacheOptions options = new()
        {
            DefaultTimeToLive = TimeSpan.FromHours(8),
            DiskCachePath = $"{SaveService.MAIN_DIRECTORY}/cache",
            DiskSyncInterval = TimeSpan.FromMinutes(2),
            EnableDiskCache = true
        };
        
        _cache = new GenericCacheService<int, MalAnimeDetails, AnimeField>(FetchFields, options);
        _tokenService = tokenService;
        _saveService = saveService;
    }


    public void Init(string? accessToken)
    {
        _client = new();
        
        if (accessToken != null)
        {
            _accessToken = accessToken;
            IS_LOGGED_IN = true;
            _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_accessToken}");
        }
        else
        {
            _accessToken = string.Empty;
            IS_LOGGED_IN = false;
            _client.DefaultRequestHeaders.Add("X-MAL-Client-ID", _tokenService.ClientId);
        }
    }

    public void SubscribeToFieldChange(int animeId, FieldChangeHandler<MalAnimeDetails> handler, params AnimeField[] fields)
    {
        _cache.SubscribeToFieldChange(animeId, handler, fields);
    }

    public void UnsubscribeFromFieldChange(int animeId, FieldChangeHandler<MalAnimeDetails> handler, params AnimeField[] fields)
    {
        _cache.UnsubscribeFromFieldChange(animeId, handler, fields);
    }
    
    private async Task<HttpResponseMessage> GetAsync(string url, string message)
    {
        _requestTimestamps.Enqueue(DateTime.Now);
        while (_requestTimestamps.Count > 3 && (DateTime.Now - _requestTimestamps.Peek()).TotalSeconds < 1)
        {
            await Task.Delay(500);
        }
        if (_requestTimestamps.Count > 3)
        {
            _requestTimestamps.Dequeue();
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
            throw new HttpRequestException($"API returned status code: {response.StatusCode}");
        }

        string responseBody = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(responseBody, _jso);
    }

    #region Require Login

    public async Task<MAL_UserData> GetUserDataAsync()
    {
        if (!IS_LOGGED_IN) return new();
        
        var userData = await GetAndDeserializeAsync<MAL_UserData>("https://api.myanimelist.net/v2/users/@me", "GetUserDataAsync");
        return userData ?? throw new InvalidOperationException("Failed to deserialize user data");
    }

    public async Task<List<MalAnimeData>> GetUserAnimeList(AnimeStatusApi status = AnimeStatusApi.none)
    {
        if (!IS_LOGGED_IN) return new();
        
        if (_userAnimeList != null)
        {
            return status == AnimeStatusApi.none 
                ? _userAnimeList 
                : _userAnimeList.Where(a => a.ListStatus?.Status == status).ToList();
        }

        try
        {
            List<MalAnimeData> animeList = new();
            string baseUrl = $"https://api.myanimelist.net/v2/users/@me/animelist?fields={MAL_NODE_FIELDS}&limit=1000&nsfw=true";
            
            if (status != AnimeStatusApi.none)
            {
                baseUrl += $"&status={status}";
            }

            string? nextPageUrl = baseUrl;
            
            while (nextPageUrl != null)
            {
                var response = await GetAndDeserializeAsync<MalUserAnimeListResponse>(nextPageUrl, "GetUserAnimeList");
                if (response?.Data != null)
                {
                    animeList.AddRange(response.Data);
                    foreach (var data in response.Data)
                    {
                        _cache.UpdatePartial(data.Node.Id, data.Node, MAL_NODE_FIELD_TYPES);
                    }
                }
                nextPageUrl = response?.Paging?.Next;
            }

            if (status == AnimeStatusApi.none)
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
    
    private async Task SetMyListStatusField(int  animeId, string fieldName, string value)
    {
        if (!IS_LOGGED_IN)
        {
            return;
        }
        
        var formData = new Dictionary<string, string> { [fieldName] = value };
        
        var content = new FormUrlEncodedContent(formData);
        var response = await _client.PutAsync($"https://api.myanimelist.net/v2/anime/{animeId}/my_list_status", content);

        if (!response.IsSuccessStatusCode)
        {
            throw new($"Failed to update anime: {response.StatusCode}");
        }

        string responseBody = await response.Content.ReadAsStringAsync();
        var myListStatus = JsonSerializer.Deserialize<MalMyListStatus>(responseBody, _jso);

        if (myListStatus != null)
        {
            var anime = await BuildAnimeFromCache(animeId);
            anime.MyListStatus = myListStatus;
            _cache.Update(anime.Id, anime, (AnimeField[])Enum.GetValues(typeof(AnimeField)));
        }

        _userAnimeList = null;
    }
    
    public async Task RemoveFromUserList(int animeId)
    {
        if(!IS_LOGGED_IN) return;
        
        var response = await _client.DeleteAsync($"https://api.myanimelist.net/v2/anime/{animeId}/my_list_status");

        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            throw new($"Failed to remove anime from list: {response.StatusCode}");
        }

        //TODO IMPORTANT ADD A WAY TO ONLY UPDATE ONE FIELD, BY PASSING ID AND ENUM
        var anime = await BuildAnimeFromCache(animeId);
        anime.MyListStatus = null;
        _cache.Update(anime.Id, anime, (AnimeField[])Enum.GetValues(typeof(AnimeField)));

        _userAnimeList = null;
    }

    #endregion
    
    private async Task<MalAnimeDetails> BuildAnimeFromCache(int id)
    {
        return await _cache.GetOrFetchFieldsAsync(id, (AnimeField[])Enum.GetValues(typeof(AnimeField)));
    }

    public async Task<MalAnimeDetails> GetFieldsAsync(int animeId, params AnimeField[] fields)
    {
        return await _cache.GetOrFetchFieldsAsync(animeId, fields: fields);
    }

    public async Task<MalAnimeDetails> GetAllFieldsAsync(int animeId)
    {
        return await _cache.GetOrFetchFieldsAsync(animeId, fields: (AnimeField[])Enum.GetValues(typeof(AnimeField)));
    }

    private async Task<MalAnimeDetails?> FetchFields(int id, params AnimeField[] fields)
    {
        StringBuilder urlFields = new();
        Bitmap? picture = null;
        foreach (var field in fields)
        {
            if (field is AnimeField.MAIN_PICTURE && _saveService.TryGetAnimeImage(id, out picture)) {}
            else if (field is not (AnimeField.PICTURE or AnimeField.TRAILER_URL or AnimeField.ID))
            {
                urlFields.Append($"{FieldToString(field)},");
            }
        }
        
        string url = $"https://api.myanimelist.net/v2/anime/{id}?fields={urlFields}&nsfw=true";
        
        var animeResponse = await GetAndDeserializeAsync<MalAnimeDetails>(url, $"FetchFields {urlFields}");

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
                animeResponse.Picture = await LoadAndCacheAnimeImage(id, animeResponse.MainPicture);
            }
                
            return animeResponse;
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
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null)
        };
    }

    private async Task<Bitmap?> LoadAndCacheAnimeImage(int id, MalMainPicture? mainPicture)
    {
        if (_saveService.TryGetAnimeImage(id, out Bitmap? bitmap))
        {
            return bitmap;
        }
        else
        {
            var downloadedImage = await GetAnimeImage(mainPicture);
            if (downloadedImage != null)
            {
                _saveService.SaveImage(id, downloadedImage);
                return downloadedImage;
            }
        }

        return null;
    }

    public async Task<Bitmap?> GetAnimeImage(MalMainPicture? animePictureData)
    {
        if (animePictureData == null) return null;

        try
        {
            return await DownloadImageAsync(animePictureData.Large);
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

    public async Task<List<MalSearchEntry>> SearchAnimeOrdered(string query)
    {
        string url = $"https://api.myanimelist.net/v2/anime?q={Uri.EscapeDataString(query)}&limit=20&fields={MAL_NODE_FIELDS}&nsfw=true";

        var responseData = await GetAndDeserializeAsync<MalAnimeSearchListResponse>(url, "SearchAnimeOrdered");

        if (responseData != null)
        {
            foreach (var data in responseData.Data)
            {
                _cache.UpdatePartial(data.Node.Id, data.Node, MAL_NODE_FIELD_TYPES);
            }
        }   

        var results = responseData?.Data?
            .Select(x => new { Entry = x, Score = CalculateSearchScore(x.Node, query) })
            .OrderByDescending(x => x.Score)
            .Select(x => x.Entry)
            .ToList() ?? new List<MalSearchEntry>();

        return results;
    }

    private int CalculateSearchScore(MalAnimeNode anime, string query)
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

    private bool DoesTitleMatch(MalAnimeNode malAnime, string query)
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

    public async Task<List<MAL_RankingEntry>> GetTopAnimeInCategory(AnimeRankingCategory category, int limit = 10)
    {
        string rankingType = category switch
        {
            AnimeRankingCategory.AIRING => "airing",
            AnimeRankingCategory.UPCOMING => "upcoming",
            AnimeRankingCategory.ALLTIME => "all",
            AnimeRankingCategory.BYPOPULARITY => "bypopularity",
            _ => "all"
        };

        string url = $"https://api.myanimelist.net/v2/anime/ranking?ranking_type={rankingType}&limit={limit}&fields={MAL_NODE_FIELDS}&nsfw=true";

        var response = await GetAndDeserializeAsync<MAL_AnimeRankingResponse>(url, "GetTopAnimeInCategory");
        
        if (response?.Data != null)
        {
            foreach (var data in response.Data)
            {
                _cache.UpdatePartial(data.Node.Id, data.Node, MAL_NODE_FIELD_TYPES);
            }
            
            return response.Data.ToList();
        }

        return new List<MAL_RankingEntry>();
    }

    public Task SetAnimeStatus(int animeId, AnimeStatusApi status) 
        => SetMyListStatusField(animeId, "status", status.ToString());

    public Task SetAnimeScore(int animeId, int score) 
        => SetMyListStatusField(animeId, "score", score.ToString());

    public Task SetEpisodesWatched(int animeId, int episodes) 
        => SetMyListStatusField(animeId, "num_watched_episodes", episodes.ToString());
    
    private async Task<string?> GetAnimeTrailerUrlJikan(int animeId)
    {
        try
        {
            string url = $"https://api.jikan.moe/v4/anime/{animeId}/videos";

            var response = await GetAsync(url, "GetAnimeTrailerUrlJikan");

            if (!response.IsSuccessStatusCode)
            {
                Log.Information($"Failed to get trailer from Jikan: {response.StatusCode}");
                return null;
            }

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("data", out var dataElem) &&
                dataElem.TryGetProperty("promo", out var promoArray) &&
                promoArray.ValueKind == JsonValueKind.Array &&
                promoArray.GetArrayLength() > 0)
            {
                var firstPromo = promoArray[0];
                if (firstPromo.TryGetProperty("trailer", out var trailerElem))
                {
                    if (trailerElem.TryGetProperty("embed_url", out var urlElem))
                    {
                        return urlElem.GetString();
                    }
                    if (trailerElem.TryGetProperty("youtube_id", out var youtubeIdElem))
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
}