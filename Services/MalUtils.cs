using System.Diagnostics;
using Aniki.Misc;
using Avalonia.Media.Imaging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aniki.Services;

public static class MalUtils
{
    public enum AnimeStatusField { STATUS, SCORE, EPISODES_WATCHED }
    
    private static JsonSerializerOptions _jso = new() { PropertyNameCaseInsensitive = true };
    private static HttpClient _client = new();
    
    private static readonly Dictionary<int, AnimeDetails> _detailsCache = new();
    private static List<AnimeData>? _userAnimeList;

    private static Stopwatch sw = new();
    private static int requestCounter;
    private static Queue<DateTime> _requestTimestamps = new();
    
    public static void Init(string? accessToken)
    {
        _client = new();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
    }

    private static async Task<HttpResponseMessage> GetAsync(string url, string message)
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
        sw = Stopwatch.StartNew();
#endif
        HttpResponseMessage result = await _client.GetAsync(url);
#if DEBUG
        sw.Stop();
        Log.Information($"{requestCounter}: {message} took: {sw.ElapsedMilliseconds}");
#endif
        requestCounter++;
        return result;
    }

    public static async Task<MALUserData> GetUserDataAsync()
    {
        string? accessToken = TokenService.GetAccessToken();
        if (string.IsNullOrEmpty(accessToken))
        {
            throw new InvalidOperationException("No access token available");
        }

        HttpResponseMessage response = await GetAsync("https://api.myanimelist.net/v2/users/@me", "GetUserDataAsync");

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to get user data: {response.StatusCode}");
        }

        string json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<MALUserData>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) 
               ?? throw new InvalidOperationException("Failed to deserialize data");
    }

    public static async Task<List<AnimeData>> GetUserAnimeList(AnimeStatusApi status = AnimeStatusApi.none)
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
            List<AnimeData> animeList = new();

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
                    UserAnimeListResponse? animeListResponse = JsonSerializer.Deserialize<UserAnimeListResponse>(responseBody, _jso);

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
                                var basicDetails = new AnimeDetails
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

    public static async Task<AnimeDetails?> GetAnimeDetails(int id, bool forceFull = false)
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

    private static async Task<AnimeDetails?> FetchFullAnimeDetails(int id)
    {
        try
        {
            string url = $"https://api.myanimelist.net/v2/anime/{id}?fields=id,title,main_picture,status,synopsis,my_list_status,num_episodes,related_anime{{id,title,num_episodes,media_type,synopsis,status,alternative_titles}},genres,alternative_titles";

            HttpResponseMessage response = await GetAsync(url, "Details");
            string responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                AnimeDetails? animeResponse = JsonSerializer.Deserialize<AnimeDetails>(responseBody, _jso);
                if (animeResponse != null)
                {
                    _detailsCache[id] = animeResponse;
                    animeResponse.Picture = await SaveService.GetAnimeImage(animeResponse);
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

    public static async Task<string> GetAnimeNameById(int id)
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
                AnimeDetails? animeResponse = JsonSerializer.Deserialize<AnimeDetails>(responseBody, _jso);
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

    public static async Task<Bitmap?> GetUserPicture()
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

    public static async Task<Bitmap?> GetAnimeImage(MainPicture? animePictureData)
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

    public static async Task<List<SearchEntry>> SearchAnimeOrdered(string query)
    {
        string url = $"https://api.myanimelist.net/v2/anime?q={Uri.EscapeDataString(query)}&limit=20&fields=id,title,main_picture,num_episodes,main_picture,synopsis,status,alternative_titles";

        HttpResponseMessage response = await GetAsync(url, "Search");
        string responseBody = await response.Content.ReadAsStringAsync();
        AnimeSearchListResponse? responseData = JsonSerializer.Deserialize<AnimeSearchListResponse>(responseBody, _jso);

        var results = responseData?.Data?.Select(x =>
        {
            if (DoesTitleMatch(x.Anime, query))
            {
                return new { Entry = x, Score = 1000 };
            }
            
            int score = FuzzySharp.Fuzz.TokenSortRatio(x.Anime.Title, query);
            if (x.Anime.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                score += 50;
            }

            if (x.Anime?.Id != null && !_detailsCache.ContainsKey(x.Anime.Id))
            {
                _detailsCache[x.Anime.Id] = new AnimeDetails()
                {
                    Id = x.Anime.Id,
                    Title = x.Anime.Title,
                    Genres = x.Anime.Genres,
                    MainPicture = x.Anime.MainPicture,
                    Status = x.Anime.Status,
                    Synopsis = x.Anime.Synopsis,
                    NumEpisodes = x.Anime.NumEpisodes,
                    AlternativeTitles = x.Anime.AlternativeTitles
                };
            }

            return new { Entry = x, Score = score };
        })
        .OrderByDescending(x => x.Score)
        .Select(x => x.Entry)
        .ToList() ?? new List<SearchEntry>();

        return results;
    }

    private static bool DoesTitleMatch(AnimeNode anime, string query)
    {
        string normalizedQuery = NormalizeTitleToLower(query);
        string normalizedTitle = NormalizeTitleToLower(anime.Title);
        
        if(normalizedTitle == anime.Title) return true;
        
        if(anime.AlternativeTitles == null) return false;
        
        string normalizedEn = NormalizeTitleToLower(anime.AlternativeTitles.En);
        string normalizedJp = NormalizeTitleToLower(anime.AlternativeTitles.Ja);
        
        if(normalizedJp == normalizedQuery) return true;
        if(normalizedEn == normalizedQuery) return true;
        
        if(anime.AlternativeTitles.Synonyms != null && anime.AlternativeTitles.Synonyms.Any(x => NormalizeTitleToLower(x) == normalizedQuery)) 
            return true;
        
        return false; 
    }

    private static string NormalizeTitleToLower(string? title)
    {
        if (string.IsNullOrEmpty(title)) return string.Empty;
    
        string normalized = title.Replace("-", "").Replace("_", "").Replace(":", "").Trim();
        normalized = Regex.Replace(normalized, @"\s+", " ");
        return normalized.ToLower();
    }

    public static async Task UpdateAnimeStatus(int animeId, AnimeStatusField field, string value)
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
                cached.MyListStatus = new MyListStatus();

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

    public static async Task RemoveFromList(int animeId)
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

    private static async void OnEpisodesWatchedChanged(int animeId, string value)
    {
        string animeTitle = await GetAnimeNameById(animeId);
        SaveService.ChangeWatchingAnimeEpisode(animeTitle, int.Parse(value));
    }

    private static async void OnStatusChanged(int animeId, string value)
    {
        string animeTitle = await GetAnimeNameById(animeId);
        SaveService.ChangeWatchingAnimeStatus(animeTitle, StatusEnum.StringToApi(value));
    }
}