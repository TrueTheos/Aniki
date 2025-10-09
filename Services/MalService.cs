using System.Diagnostics;
using Aniki.Misc;
using Avalonia.Media.Imaging;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aniki.Services.Interfaces;

namespace Aniki.Services;

public class MalService : IMalService
{
    public enum AnimeStatusField { STATUS, SCORE, EPISODES_WATCHED }
    public enum AnimeRankingCategory { AIRING, UPCOMING, ALLTIME }
    
    private JsonSerializerOptions _jso = new() { PropertyNameCaseInsensitive = true };
    private HttpClient _client = new();
    
    private readonly Dictionary<int, MAL_AnimeDetails> _detailsCache = new();
    private List<MAL_AnimeData>? _userAnimeList;

    private Stopwatch _sw = new();
    private int _requestCounter;
    private readonly Queue<DateTime> _requestTimestamps = new();
    
    private readonly ISaveService _saveService;
    
    private string _accessToken = "";

    public MalService(ISaveService saveService)
    {
        _saveService = saveService;
    }
    
    public void Init(string accessToken)
    {
        _accessToken = accessToken;
        _client = new();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_accessToken}");
    }
    
    private async Task<HttpResponseMessage> GetAsync(string url, string message)
    {
        _requestTimestamps.Enqueue(DateTime.Now);
        while (_requestTimestamps.Count > 3 && _requestTimestamps.Peek() > DateTime.Now.Subtract(TimeSpan.FromSeconds(1)))
        {
            await Task.Delay(500);
        }
        if (_requestTimestamps.Count > 3)
        {
            _requestTimestamps.Dequeue();
        }

#if DEBUG
        _sw = Stopwatch.StartNew();
#endif
        HttpResponseMessage result = await _client.GetAsync(url);
#if DEBUG
        _sw.Stop();
        Log.Information($"{_requestCounter}: {message} took: {_sw.ElapsedMilliseconds}");
#endif
        _requestCounter++;
        return result;
    }

    public  async Task<MAL_UserData> GetUserDataAsync()
    {
        if (string.IsNullOrEmpty(_accessToken))
        {
            throw new InvalidOperationException("No access token available");
        }

        HttpResponseMessage response = await GetAsync("https://api.myanimelist.net/v2/users/@me", "GetUserDataAsync");

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to get user data: {response.StatusCode}");
        }

        string json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<MAL_UserData>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) 
               ?? throw new InvalidOperationException("Failed to deserialize data");
    }

    public  async Task<List<MAL_AnimeData>> GetUserAnimeList(AnimeStatusApi status = AnimeStatusApi.none)
    {
        if (_userAnimeList != null)
        {
            if (status == AnimeStatusApi.none)
            {
                return _userAnimeList;
            }
            else
            {
                return _userAnimeList.Where(a => a.ListStatus?.Status == status).ToList();
            }
        }

        try
        {
            List<MAL_AnimeData> animeList = new();

            string url = "https://api.myanimelist.net/v2/users/@me/animelist?";
            string fields = $"list_status,num_episodes,pictures,status,genres,synopsis,main_picture";
                
            url += $"fields={fields}&limit=1000";

            if (status != AnimeStatusApi.none)
            {
                url += $"&status={status}";
            }
                
            bool hasNextPage = true;
            string nextPageUrl = url;

            while (hasNextPage)
            {
                HttpResponseMessage response = await GetAsync(nextPageUrl, "GetUserAnimeList");
                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    MAL_UserAnimeListResponse? animeListResponse = JsonSerializer.Deserialize<MAL_UserAnimeListResponse>(responseBody, _jso);

                    if (animeListResponse == null)
                    {
                        hasNextPage = false;
                        continue;
                    }
                        
                    if (animeListResponse.Data != null)
                    {
                        animeList.AddRange(animeListResponse.Data);
                        
                        foreach (var animeData in animeListResponse.Data)
                        {
                            if (animeData.Node?.Id != null && !_detailsCache.ContainsKey(animeData.Node.Id))
                            {
                                var basicDetails = new MAL_AnimeDetails
                                {
                                    Id = animeData.Node.Id,
                                    Title = animeData.Node.Title,
                                    MainPicture = animeData.Node.MainPicture,
                                    Status = animeData.Node.Status,
                                    Synopsis = animeData.Node.Synopsis,
                                    NumEpisodes = animeData.Node.NumEpisodes,
                                    MyListStatus = animeData.ListStatus,
                                    Genres = animeData.Node.Genres
                                };
                                _detailsCache[animeData.Node.Id] = basicDetails;
                            }
                        }
                    }

                    if (animeListResponse.Paging != null && !string.IsNullOrEmpty(animeListResponse.Paging.Next))
                    {
                        nextPageUrl = animeListResponse.Paging.Next;
                    }
                    else
                    {
                        hasNextPage = false;
                    }
                }
                else
                {
                    hasNextPage = false;
                    throw new($"API returned status code: {response.StatusCode}");
                }
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

    public  async Task<MAL_AnimeDetails?> GetAnimeDetails(int id, bool forceFull = false)
    {
        if (_detailsCache.TryGetValue(id, out var cached))
        {
            if (!forceFull || cached.RelatedAnime != null)
            {
                return cached;
            }
        }
        
        return await FetchFullAnimeDetails(id);
    }

    private async Task<MAL_AnimeDetails?> FetchFullAnimeDetails(int id)
    {
        try
        {
            string url = $"https://api.myanimelist.net/v2/anime/{id}?fields=id,title,main_picture,status,synopsis,my_list_status,num_episodes,related_anime{{id,title,num_episodes,media_type,synopsis,status,alternative_titles}},genres,alternative_titles";

            HttpResponseMessage response = await GetAsync(url, "Details");
            string responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                MAL_AnimeDetails? animeResponse = JsonSerializer.Deserialize<MAL_AnimeDetails>(responseBody, _jso);
                if (animeResponse != null)
                {
                    _detailsCache[id] = animeResponse;
                    var pic = _saveService.TryGetAnimeImage(animeResponse.Id);
                    if (pic != null)
                    {
                        animeResponse.Picture = _saveService.TryGetAnimeImage(animeResponse.Id);
                    }
                    else
                    {
                        Bitmap? downloadedImage = await GetAnimeImage(animeResponse.MainPicture);
                        if (downloadedImage != null)
                        {
                            _saveService.SaveImage(animeResponse.Id, downloadedImage);
                            animeResponse.Picture = downloadedImage;;
                        }
                    }

                    return animeResponse;
                }
            }
            else
            {
                throw new($"API returned status code: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            throw new($"Error loading anime details: {ex.Message}", ex);
        }
            
        return null;
    }

    public  async Task<string> GetAnimeNameById(int id)
    {
        if (_detailsCache.TryGetValue(id, out var cached))
        {
            return cached.Title ?? "";
        }

        try
        {
            string url = $"https://api.myanimelist.net/v2/anime/{id}?fields=title";
            HttpResponseMessage response = await GetAsync(url, "TitleById");
            string responseBody = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                MAL_AnimeDetails? animeResponse = JsonSerializer.Deserialize<MAL_AnimeDetails>(responseBody, _jso);
                if (animeResponse != null)
                {
                    if (!_detailsCache.ContainsKey(id))
                    {
                        _detailsCache[id] = animeResponse;
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(_detailsCache[id].Title))
                        {
                            _detailsCache[id].Title = animeResponse.Title;
                        }
                    }
                    return animeResponse.Title ?? "";
                }
            }
            else
            {
                throw new($"API returned status code: {response.StatusCode}");
            }
        }
        catch (Exception) { }

        return "";
    }

    public  async Task<Bitmap?> GetUserPicture()
    {
        try
        {
            HttpResponseMessage response = await GetAsync("https://api.myanimelist.net/v2/users/@me?fields=picture", "UserPicture");
            string responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                using JsonDocument doc = JsonDocument.Parse(responseBody);
                JsonElement root = doc.RootElement;

                if (root.TryGetProperty("picture", out JsonElement pictureElement))
                {
                    string pictureUrl = pictureElement.ToString();

                    byte[] imageData = await _client.GetByteArrayAsync(pictureUrl);

                    using MemoryStream ms = new(imageData);
                    return new(ms);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Information($"Error getting profile picture URL: {ex.Message}");
        }

        return null;
    }

    public  async Task<Bitmap?> GetAnimeImage(MAL_MainPicture? animePictureData)
    {
        try
        {
            if(animePictureData == null) return null;
            byte[] imageData = await _client.GetByteArrayAsync(animePictureData.Medium);

            using MemoryStream ms = new(imageData);
            return new(ms);
        }
        catch (Exception ex)
        {
            Log.Information($"Error getting anime picture: {ex.Message}");
        }

        return null;
    }

    public  async Task<List<MAL_SearchEntry>> SearchAnimeOrdered(string query)
    {
        string url = $"https://api.myanimelist.net/v2/anime?q={Uri.EscapeDataString(query)}&limit=20&fields=id,title,main_picture,num_episodes,main_picture,synopsis,status,alternative_titles";

        HttpResponseMessage response = await GetAsync(url, "Search");
        string responseBody = await response.Content.ReadAsStringAsync();
        MAL_AnimeSearchListResponse? responseData = JsonSerializer.Deserialize<MAL_AnimeSearchListResponse>(responseBody, _jso);

        var results = responseData?.Data?.Select(x =>
        {
            if (DoesTitleMatch(x.MalAnime, query))
            {
                return new { Entry = x, Score = 1000 };
            }
            
            int score = FuzzySharp.Fuzz.TokenSortRatio(x.MalAnime.Title, query);
            if (x.MalAnime.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                score += 50;
            }

            if (x.MalAnime?.Id != null && !_detailsCache.ContainsKey(x.MalAnime.Id))
            {
                _detailsCache[x.MalAnime.Id] = new MAL_AnimeDetails()
                {
                    Id = x.MalAnime.Id,
                    Title = x.MalAnime.Title,
                    Genres = x.MalAnime.Genres,
                    MainPicture = x.MalAnime.MainPicture,
                    Status = x.MalAnime.Status,
                    Synopsis = x.MalAnime.Synopsis,
                    NumEpisodes = x.MalAnime.NumEpisodes,
                    AlternativeTitles = x.MalAnime.AlternativeTitles
                };
            }

            return new { Entry = x, Score = score };
        })
        .OrderByDescending(x => x.Score)
        .Select(x => x.Entry)
        .ToList() ?? new List<MAL_SearchEntry>();

        return results;
    }

    private  bool DoesTitleMatch(MAL_AnimeNode malAnime, string query)
    {
        string normalizedQuery = NormalizeTitleToLower(query);
        string normalizedTitle = NormalizeTitleToLower(malAnime.Title);
        
        if(normalizedTitle == malAnime.Title) return true;
        
        if(malAnime.AlternativeTitles == null) return false;
        
        string normalizedEn = NormalizeTitleToLower(malAnime.AlternativeTitles.En);
        string normalizedJp = NormalizeTitleToLower(malAnime.AlternativeTitles.Ja);
        
        if(normalizedJp == normalizedQuery) return true;
        if(normalizedEn == normalizedQuery) return true;
        
        if(malAnime.AlternativeTitles.Synonyms != null && malAnime.AlternativeTitles.Synonyms.Any(x => NormalizeTitleToLower(x) == normalizedQuery)) 
            return true;
        
        return false; 
    }

    private  string NormalizeTitleToLower(string? title)
    {
        if (string.IsNullOrEmpty(title)) return string.Empty;
    
        string normalized = title.Replace("-", "").Replace("_", "").Replace(":", "").Trim();
        normalized = Regex.Replace(normalized, @"\s+", " ");
        return normalized.ToLower();
    }

    public  async Task UpdateAnimeStatus(int animeId, AnimeStatusField field, string value)
    {
        string url = $"https://api.myanimelist.net/v2/anime/{animeId}/my_list_status";

        Dictionary<string, string> formData = new Dictionary<string, string>();
        switch (field)
        {
            case AnimeStatusField.STATUS:
                formData["status"] = value;
                OnStatusChanged(animeId, value);
                break;
            case AnimeStatusField.SCORE:
                formData["score"] = value;
                break;
            case AnimeStatusField.EPISODES_WATCHED:
                formData["num_watched_episodes"] = value;
                OnEpisodesWatchedChanged(animeId, value);
                break;
        }

        FormUrlEncodedContent content = new(formData);

        HttpResponseMessage response = await _client.PutAsync(url, content);

        if (!response.IsSuccessStatusCode)
        {
            throw new($"Failed to update anime status: {response.StatusCode}");
        }

        if (_detailsCache.TryGetValue(animeId, out var cached))
        {
            if (cached.MyListStatus == null)
                cached.MyListStatus = new MAL_MyListStatus();

            switch (field)
            {
                case AnimeStatusField.STATUS:
                    cached.MyListStatus.Status = StatusEnum.StringToApi(value);
                    break;
                case AnimeStatusField.SCORE:
                    if (int.TryParse(value, out int score))
                        cached.MyListStatus.Score = score;
                    break;
                case AnimeStatusField.EPISODES_WATCHED:
                    if (int.TryParse(value, out int episodes))
                        cached.MyListStatus.NumEpisodesWatched = episodes;
                    break;
            }
        }

        _userAnimeList = null;
    }

    public async Task RemoveFromList(int animeId)
    {
        string url = $"https://api.myanimelist.net/v2/anime/{animeId}/my_list_status";

        HttpResponseMessage response = await _client.DeleteAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Log.Information($"Anime with ID {animeId} not found on the user's list, or already deleted.");
                return;
            }
            throw new($"Failed to remove anime from list: {response.StatusCode}");
        }

        if (_detailsCache.TryGetValue(animeId, out var cached))
        {
            cached.MyListStatus = null;
        }

        _userAnimeList = null;
    }

    public async Task<List<MAL_AnimeSearchListResponse>> GetTopAnimeInCategory(AnimeRankingCategory category)
    {
        
    }

    private async void OnEpisodesWatchedChanged(int animeId, string value)
    {
        string animeTitle = await GetAnimeNameById(animeId);
    }

    private  async void OnStatusChanged(int animeId, string value)
    {
        string animeTitle = await GetAnimeNameById(animeId);
    }
}