using Avalonia.Controls.Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Aniki.Services
{
    namespace Aniki.Services
    {
        public class EpisodeNotificationService
        {
            private readonly HttpClient _httpClient;
            private readonly CancellationTokenSource _cancellationTokenSource;

            public EpisodeNotificationService(INotificationManager notificationManager)
            {
                _httpClient = new();
                _cancellationTokenSource = new();
            }

            public void Start()
            {
                Task.Run(async () => await CheckForNewEpisodesAsync(_cancellationTokenSource.Token));
            }

            public void Stop()
            {
                _cancellationTokenSource.Cancel();
            }

            private async Task CheckForNewEpisodesAsync(CancellationToken cancellationToken)
            {
                System.Diagnostics.Debug.WriteLine("Start");
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine("Fetching currently watching anime...");
                        SaveService.LoadAnimeStatuses();
                        List<SaveService.AnimeStatus> watching = SaveService.AnimeStatuses.Where(a => a.Status == Misc.AnimeStatusApi.watching).ToList();
                        System.Diagnostics.Debug.WriteLine($"Found {watching.Count} anime(s) currently being watched.");

                        List<AiringSchedule>? airingData = await FetchAiringScheduleFromAniList();

                        foreach (SaveService.AnimeStatus anime in watching)
                        {
                            AiringSchedule? matchingAnime = airingData?.FirstOrDefault(a => a.Title == anime.Title);
                            if (matchingAnime != null && matchingAnime.NextEpisodeDate <= DateTime.Now)
                            {
                                //Notify
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"FAILED: {ex}");
                    }

                    await Task.Delay(TimeSpan.FromMinutes(10), cancellationToken);
                }
            }

            private async Task<List<AiringSchedule>?> FetchAiringScheduleFromAniList()
            {
                const string query = @"
                    query {
                       Page(page: 1, perPage: 50) {
                           airingSchedules {
                               media {
                                   title {
                                       romaji
                                   }
                               }
                               episode
                               airingAt
                           }
                       }
                    }";
                
                var requestBody = new { query };
                StringContent requestContent = new(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                HttpResponseMessage response = await _httpClient.PostAsync("https://graphql.anilist.co", requestContent);
                response.EnsureSuccessStatusCode();

                string responseString = await response.Content.ReadAsStringAsync();
                JsonArray? schedules = JsonNode.Parse(responseString)?["data"]?["Page"]?["airingSchedules"]?.AsArray();
   
                return schedules?.Select(s => new AiringSchedule
                {
                    Title = s?["media"]?["title"]?["romaji"]?.GetValue<string>() ?? "",
                    NextEpisodeDate = DateTimeOffset.FromUnixTimeSeconds(s?["airingAt"]?.GetValue<long>() ?? 0).DateTime
                }).ToList();
            }

            private class AiringSchedule
            {
                public required string Title { get; init; }
                public DateTime NextEpisodeDate { get; init; }
            }
        }
    }
}
