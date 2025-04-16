using Avalonia.Controls;
using Avalonia.Media.Imaging;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Aniki
{
    public static class MalUtils
    {
        private static JsonSerializerOptions _jso = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private static readonly HttpClient _client = new HttpClient();

        public static void Init(string accessToken)
        {
            _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
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
                string fields = "list_status,num_episodes,synopsis,mean,media_type,status,studios,pictures";

                if (!string.IsNullOrEmpty(status) && status != "All")
                {
                    // Convert display name to API parameter
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

                        // Check if there's a next page
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

        public static async Task<Bitmap> GetUserPicture()
        {
            try
            {
                HttpResponseMessage response = await _client.GetAsync("https://api.myanimelist.net/v2/users/@me?fields=picture");
                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // Parse the JSON response to get the picture URL
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

    public class UserData
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class AnimeListResponse
    {
        public AnimeData[] Data { get; set; }
        public Paging Paging { get; set; }
    }

    public class AnimeData
    {
        public AnimeNode Node { get; set; }
        [JsonPropertyName("list_status")]
        public ListStatus ListStatus { get; set; }
    }

    public class AnimeNode
    {
        public int Id { get; set; }
        public string Title { get; set; }
    }

    public class ListStatus
    {
        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("score")]
        public int Score { get; set; }

        [JsonPropertyName("num_episodes_watched")]
        public int Num_Episodes_Watched { get; set; }
    }

    public class Paging
    {
        public string Next { get; set; }
    }
}
