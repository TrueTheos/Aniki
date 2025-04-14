using Avalonia.Controls;
using Avalonia.Media.Imaging;
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
        /// <summary>
        /// Loads user data from MyAnimeList API
        /// </summary>
        /// <param name="accessToken">MAL API access token</param>
        /// <returns>User name</returns>
        public static async Task<UserData> LoadUserData(string accessToken)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                    HttpResponseMessage response = await client.GetAsync("https://api.myanimelist.net/v2/users/@me?fields=name");
                    string responseBody = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        return JsonSerializer.Deserialize<UserData>(responseBody,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    }

                    throw new Exception($"API returned status code: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error loading user data: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Loads anime list from MyAnimeList API
        /// </summary>
        /// <param name="accessToken">MAL API access token</param>
        /// <param name="status">Filter by status (optional)</param>
        /// <returns>Collection of anime data</returns>
        public static async Task<List<AnimeData>> LoadAnimeList(string accessToken, string status = null)
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

                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                    bool hasNextPage = true;
                    string nextPageUrl = url;

                    while (hasNextPage)
                    {
                        HttpResponseMessage response = await client.GetAsync(nextPageUrl);
                        string responseBody = await response.Content.ReadAsStringAsync();

                        if (response.IsSuccessStatusCode)
                        {
                            var animeListResponse = JsonSerializer.Deserialize<AnimeListResponse>(responseBody,
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

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
                }

                return animeList;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error loading anime list: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Converts display status to API parameter
        /// </summary>
        /// <param name="displayStatus">Display status from UI</param>
        /// <returns>API status parameter</returns>
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
