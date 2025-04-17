using Aniki.Models;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Aniki.Services
{
    public static class MalUtils
    {
        private static JsonSerializerOptions _jso = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private static readonly HttpClient _client = new HttpClient();

        public static void Init(string accessToken)
        {
            _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
        }

        public static async Task<UserData> GetUserDataAsync()
        {
            string accessToken = TokenService.GetAccessToken();
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
            return JsonSerializer.Deserialize<UserData>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        public static async Task<UserData> LoadUserData()
        {
            try
            {
                HttpResponseMessage response = await _client.GetAsync("https://api.myanimelist.net/v2/users/@me?fields=name,id");
                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return JsonSerializer.Deserialize<UserData>(responseBody, _jso);
                }

                throw new Exception($"API returned status code: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error loading user data: {ex.Message}", ex);
            }
        }

        public static async Task<List<AnimeData>> LoadAnimeList(string status = null)
        {
            try
            {
                List<AnimeData> animeList = new List<AnimeData>();

                string url = "https://api.myanimelist.net/v2/users/@me/animelist?";
                string fields = "list_status,num_episodes,status,pictures";

                if (!string.IsNullOrEmpty(status) && status != "All")
                {
                    string apiStatus = ConvertStatusToApiParameter(status);
                    url += $"status={apiStatus}&";
                }

                url += $"fields={fields}&limit=100";

                bool hasNextPage = true;
                string nextPageUrl = url;

                while (hasNextPage)
                {
                    HttpResponseMessage response = await _client.GetAsync(nextPageUrl);
                    string responseBody = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        var animeListResponse = JsonSerializer.Deserialize<AnimeListResponse>(responseBody, _jso);

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
                        throw new Exception($"API returned status code: {response.StatusCode}");
                    }
                }

                return animeList;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error loading anime list: {ex.Message}", ex);
            }
        }

        public static async Task<AnimeDetails> GetAnimeDetails(int id)
        {
            try
            {
                string url = $"https://api.myanimelist.net/v2/anime/{id}?fields=id,title,main_picture,status,synopsis,my_list_status,num_episodes";

                HttpResponseMessage response = await _client.GetAsync(url);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var animeResponse = JsonSerializer.Deserialize<AnimeDetails>(responseBody, _jso);
                    animeResponse.Picture = await ImageCache.GetAnimeImage(animeResponse);
                    return animeResponse;
                }
                else
                {
                    throw new Exception($"API returned status code: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error loading anime details: {ex.Message}", ex);
            }
        }

        public static async Task<Bitmap> GetUserPicture()
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

                        using MemoryStream ms = new MemoryStream(imageData);
                        return new Bitmap(ms);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting profile picture URL: {ex.Message}");
            }

            return null;
        }

        public static async Task<Bitmap> GetAnimeImage(MainPicture animePictureData)
        {
            try
            {
                byte[] imageData = await _client.GetByteArrayAsync(animePictureData.Medium);

                using MemoryStream ms = new MemoryStream(imageData);
                return new Bitmap(ms);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting anime picture: {ex.Message}");
            }

            return null;
        }

        private static string ConvertStatusToApiParameter(string displayStatus)
        {
            return displayStatus switch
            {
                "Currently Watching" => "watching",
                "Completed" => "completed",
                "On Hold" => "on_hold",
                "Dropped" => "dropped",
                "Plan to Watch" => "plan_to_watch",
                _ => ""
            };
        }
    }

   

    public class AnimeListResponse
    {
        public AnimeData[] Data { get; set; }
        public Paging Paging { get; set; }
    }

    public class Paging
    {
        public string Next { get; set; }
    }
}
