using Avalonia.Controls.Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static Aniki.Services.SaveService;
using System.Xml.Linq;
using Aniki.Models;
using System.Reflection.Metadata;
using Microsoft.Toolkit.Uwp.Notifications;

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
                _notificationManager = notificationManager;
                _httpClient = new HttpClient();
                _cancellationTokenSource = new CancellationTokenSource();
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
                        var watching = SaveService.AnimeStatuses.Where(a => a.Status == Misc.AnimeStatusAPI.watching).ToList();
                        System.Diagnostics.Debug.WriteLine($"Found {watching.Count} anime(s) currently being watched.");

                        var airingData = await FetchAiringScheduleFromAniList();

                        foreach (var anime in watching)
                        {
                            var matchingAnime = airingData?.FirstOrDefault(a => a.Title == anime.Title);
                            if (matchingAnime != null && matchingAnime.NextEpisodeDate <= DateTime.Now)
                            {
                                var builder = new ToastContentBuilder().AddText("New Episode Available!")
                                    .AddText($"Episode {matchingAnime.NextEpisodeNumber} of {anime.Title} is now available!");
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

            private async Task<List<AiringSchedule>> FetchAiringScheduleFromAniList()
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

                var requestBody = new
                {
                    query
                };

                var requestContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("https://graphql.anilist.co", requestContent);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                var responseData = JsonSerializer.Deserialize<AniListResponse>(responseString);

                return responseData?.Data?.Page?.AiringSchedules
                    .Select(a => new AiringSchedule
                    {
                        Title = a.Media.Title.Romaji,
                        NextEpisodeNumber = a.Episode,
                        NextEpisodeDate = DateTimeOffset.FromUnixTimeSeconds(a.AiringAt).DateTime
                    })
                    .ToList();
            }

            public class AiringSchedule
            {
                public string Title { get; set; }
                public int NextEpisodeNumber { get; set; }
                public DateTime NextEpisodeDate { get; set; }
            }

            public class AniListResponse
            {
                public AniListData Data { get; set; }
            }

            public class AniListData
            {
                public AniListPage Page { get; set; }
            }

            public class AniListPage
            {
                public List<AniListAiringSchedule> AiringSchedules { get; set; }
            }

            public class AniListAiringSchedule
            {
                public AniListMedia Media { get; set; }
                public int Episode { get; set; }
                public long AiringAt { get; set; }
            }

            public class AniListMedia
            {
                public AniListTitle Title { get; set; }
            }

            public class AniListTitle
            {
                public string Romaji { get; set; }
            }
        }
    }
}
