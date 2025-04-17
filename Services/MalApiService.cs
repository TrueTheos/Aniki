using Aniki.Models;
using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Aniki.Services
{
    public class MalApiService : IMalApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _cacheFolder;

        public MalApiService()
        {
            _httpClient = new HttpClient();

            _cacheFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Aniki", "Cache");

            if (!Directory.Exists(_cacheFolder))
            {
                Directory.CreateDirectory(_cacheFolder);
            }
        }

        public async Task<UserData> GetUserDataAsync()
        {
            string accessToken = TokenService.GetAccessToken();
            if (string.IsNullOrEmpty(accessToken))
            {
                throw new InvalidOperationException("No access token available");
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            HttpResponseMessage response = await _httpClient.GetAsync("https://api.myanimelist.net/v2/users/@me");

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Failed to get user data: {response.StatusCode}");
            }

            string json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<UserData>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        public async Task<List<AnimeData>> GetAnimeListAsync(string status = null)
        {
            string accessToken = TokenService.GetAccessToken();
            if (string.IsNullOrEmpty(accessToken))
            {
                throw new InvalidOperationException("No access token available");
            }

            string url = "https://api.myanimelist.net/v2/users/@me/animelist?fields=list_status";

            if (!string.IsNullOrEmpty(status) && status != "All")
            {
                string apiStatus = status switch
                {
                    "Currently Watching" => "watching",
                    "Completed" => "completed",
                    "On Hold" => "on_hold",
                    "Dropped" => "dropped",
                    "Plan to Watch" => "plan_to_watch",
                    _ => null
                };

                if (apiStatus != null)
                {
                    url += $"&status={apiStatus}";
                }
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            HttpResponseMessage response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Failed to get anime list: {response.StatusCode}");
            }

            string json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<AnimeListResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return result?.Data ?? new List<AnimeData>();
        }

        public async Task<Bitmap> GetProfileImageAsync(int userId)
        {
            string cachePath = Path.Combine(_cacheFolder, $"profile_{userId}.jpg");

            if (File.Exists(cachePath))
            {
                try
                {
                    using Stream stream = File.OpenRead(cachePath);
                    return new Bitmap(stream);
                }
                catch (Exception)
                {
                    File.Delete(cachePath);
                }
            }

            var userData = await GetUserDataAsync();
            if (string.IsNullOrEmpty(userData.Picture))
            {
                return null;
            }

            HttpResponseMessage response = await _httpClient.GetAsync(userData.Picture);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Failed to download profile image: {response.StatusCode}");
            }

            using (Stream networkStream = await response.Content.ReadAsStreamAsync())
            using (FileStream fileStream = File.Create(cachePath))
            {
                await networkStream.CopyToAsync(fileStream);
            }

            using (Stream stream = File.OpenRead(cachePath))
            {
                return new Bitmap(stream);
            }
        }

        private class AnimeListResponse
        {
            public List<AnimeData> Data { get; set; }
        }
    }
}
