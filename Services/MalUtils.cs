using Aniki.Misc;
using Avalonia.Media.Imaging;
using System.Text.Json;

namespace Aniki.Services;

public static class MalUtils
{
    private static JsonSerializerOptions _jso = new() { PropertyNameCaseInsensitive = true };
    private static HttpClient _client = new();

    public static void Init(string? accessToken)
    {
        _client = new();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
    }

    public static async Task<UserData> GetUserDataAsync()
    {
        string? accessToken = TokenService.GetAccessToken();
        if (string.IsNullOrEmpty(accessToken))
        {
            throw new InvalidOperationException("No access token available");
        }

        HttpResponseMessage response = await _client.GetAsync("https://api.myanimelist.net/v2/users/@me");

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to get user data: {response.StatusCode}");
        }

        string json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<UserData>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) 
               ?? throw new InvalidOperationException("Failed to deserialize data");
    }

    public static async Task<UserData> LoadUserData()
    {
        try
        {
            HttpResponseMessage response = await _client.GetAsync("https://api.myanimelist.net/v2/users/@me?fields=name,id,anime_statistics");
            string responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return JsonSerializer.Deserialize<UserData>(responseBody, _jso) ?? throw new InvalidOperationException("Failed to deserialize data");
            }

            throw new($"API returned status code: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            throw new($"Error loading user data: {ex.Message}", ex);
        }
    }

    public static async Task<List<AnimeData>> LoadAnimeList(AnimeStatusApi status = AnimeStatusApi.none)
    {
        try
        {
            List<AnimeData> animeList = new();

            string url = "https://api.myanimelist.net/v2/users/@me/animelist?";
            string fields = $"list_status,num_episodes,pictures,status,genres";
                
            url += $"fields={fields}&limit=100";

            if (status != AnimeStatusApi.none)
            {
                url += $"&status={status}";
            }
                
            bool hasNextPage = true;
            string nextPageUrl = url;

            while (hasNextPage)
            {
                HttpResponseMessage response = await _client.GetAsync(nextPageUrl);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    AnimeListResponse? animeListResponse = JsonSerializer.Deserialize<AnimeListResponse>(responseBody, _jso);

                    if (animeListResponse == null)
                    {
                        hasNextPage = false;
                        continue;
                    }
                        
                    if (animeListResponse.Data != null)
                    {
                        animeList.AddRange(animeListResponse.Data);
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

            return animeList;
        }
        catch (Exception ex)
        {
            throw new($"Error loading anime list: {ex.Message}", ex);
        }
    }

    public static async Task<AnimeDetails?> GetAnimeDetails(int id)
    {
        try
        {
            string url = $"https://api.myanimelist.net/v2/anime/{id}?fields=id,title,main_picture,status,synopsis,my_list_status,num_episodes";

            HttpResponseMessage response = await _client.GetAsync(url);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                AnimeDetails? animeResponse = JsonSerializer.Deserialize<AnimeDetails>(responseBody, _jso);
                if (animeResponse != null)
                {
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
        try
        {
            string url = $"https://api.myanimelist.net/v2/anime/{id}?fields=title";
            HttpResponseMessage response = await _client.GetAsync(url);
            string responseBody = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                AnimeDetails? animeResponse = JsonSerializer.Deserialize<AnimeDetails>(responseBody, _jso);
                return animeResponse?.Title ?? "";
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
            HttpResponseMessage response = await _client.GetAsync("https://api.myanimelist.net/v2/users/@me?fields=picture");
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
            Console.WriteLine($"Error getting profile picture URL: {ex.Message}");
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
            Console.WriteLine($"Error getting anime picture: {ex.Message}");
        }

        return null;
    }

    public static async Task<List<SearchEntry>> SearchAnimeOrdered(string query)
    {
        string url = $"https://api.myanimelist.net/v2/anime?q={Uri.EscapeDataString(query)}&limit=20&fields=totalepisodes";

        HttpResponseMessage response = await _client.GetAsync(url);
        string responseBody = await response.Content.ReadAsStringAsync();
        AnimeSearchListResponse? responseData = JsonSerializer.Deserialize<AnimeSearchListResponse>(responseBody, _jso);

        return responseData?.Data?.Select(x =>
        {
            int score = FuzzySharp.Fuzz.TokenSortRatio(x.Anime.Title, query);
            if (x.Anime.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                score += 50; // Add a bonus for titles that start with the query
            }
            return new { Entry = x, Score = score };
        })
        .OrderByDescending(x => x.Score)
        .Select(x => x.Entry)
        .ToList() ?? new List<SearchEntry>();
    }

    public enum AnimeStatusField
    {
        STATUS,
        SCORE,
        EPISODES_WATCHED
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
    }

    public static async Task RemoveFromList(int animeId)
    {
        string url = $"https://api.myanimelist.net/v2/anime/{animeId}/my_list_status";

        HttpResponseMessage response = await _client.DeleteAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Console.WriteLine($"Anime with ID {animeId} not found on the user's list, or already deleted.");
                return;
            }
            throw new($"Failed to remove anime from list: {response.StatusCode}");
        }
    }

    public static async Task<List<RelatedAnime>> GetRelatedAnime(int id)
    {
        try
        {
            string url = $"https://api.myanimelist.net/v2/anime/{id}?fields=related_anime";

            HttpResponseMessage response = await _client.GetAsync(url);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                using JsonDocument doc = JsonDocument.Parse(responseBody);
                JsonElement root = doc.RootElement;

                if (root.TryGetProperty("related_anime", out JsonElement relatedAnimeElement))
                {
                    return JsonSerializer.Deserialize<List<RelatedAnime>>(relatedAnimeElement.ToString(), _jso) ?? new();
                }
            }
            else
            {
                throw new($"API returned status code: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            throw new($"Error loading related anime: {ex.Message}", ex);
        }

        return new List<RelatedAnime>();
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