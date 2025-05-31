using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Aniki.Models;

namespace Aniki.Services
{
    public static class CalendarService
    {
        private static readonly string[] Weekdays =
            { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

        private const string GraphQLEndpoint = "https://graphql.anilist.co";
        private static readonly HttpClient _http = new HttpClient();

        public static async Task<List<DaySchedule>> GetWeeklyScheduleAsync(
            IEnumerable<string> watchingList,
            int perPage = 150,
            DateTime? specificWeek = null, string searchQuery = "")
        {
            // Calculate the start and end of specified week or current week
            var referenceDate = specificWeek ?? DateTime.Now;
            var startOfWeek = referenceDate.Date.AddDays(-(int)referenceDate.DayOfWeek + (int)DayOfWeek.Monday);
            var endOfWeek = startOfWeek.AddDays(7);

            var startUnix = ((DateTimeOffset)startOfWeek).ToUnixTimeSeconds();
            var endUnix = ((DateTimeOffset)endOfWeek).ToUnixTimeSeconds();

            var query = @"
              query ($page: Int, $perPage: Int, $airingAt_greater: Int, $airingAt_lesser: Int) {
                Page(page: $page, perPage: $perPage) {
                  airingSchedules(
                    airingAt_greater: $airingAt_greater, 
                    airingAt_lesser: $airingAt_lesser,
                    sort: TIME
                  ) {
                    media { 
                      id
                      title { 
                        romaji
                        english
                        native
                      }
                      coverImage {
                        medium
                        large
                        color
                      }
                      format
                      status
                      episodes
                      duration
                      genres
                      studios(isMain: true) {
                        nodes {
                          name
                        }
                      }
                      averageScore
                      description(asHtml: false)
                      season
                      seasonYear
                      type
                    }
                    episode
                    airingAt
                    timeUntilAiring
                  }
                }
              }
            ";

            var payload = new JObject
            {
                ["query"] = query,
                ["variables"] = new JObject
                {
                    ["page"] = 1,
                    ["perPage"] = perPage,
                    ["airingAt_greater"] = startUnix,
                    ["airingAt_lesser"] = endUnix
                }
            };

            var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
            var response = await _http.PostAsync(GraphQLEndpoint, content);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(body);
            var schedules = json["data"]?["Page"]?["airingSchedules"] as JArray ?? new JArray();

            var watchSet = new HashSet<string>(watchingList, StringComparer.OrdinalIgnoreCase);

            // Initialize days for the week
            var daySchedules = new List<DaySchedule>();
            for (int i = 0; i < 7; i++)
            {
                var currentDate = startOfWeek.AddDays(i);
                daySchedules.Add(new DaySchedule
                {
                    Name = currentDate.DayOfWeek.ToString(),
                    DayName = currentDate.ToString("dddd"),
                    Date = currentDate,
                    IsToday = currentDate.Date == DateTime.Today,
                    Items = new ObservableCollection<AnimeScheduleItem>()
                });
            }

            var byDay = daySchedules.ToDictionary(ds => ds.Name, StringComparer.OrdinalIgnoreCase);

            // Process schedules
            foreach (var schedule in schedules)
            {
                try
                {
                    var animeItem = ParseAnimeScheduleItem(schedule, watchSet);
                    if (animeItem != null)
                    {
                        var weekday = animeItem.AiringAt.ToString("dddd", CultureInfo.InvariantCulture);

                        if (byDay.TryGetValue(weekday, out var daySchedule))
                        {
                            // Check for duplicates (same anime, same episode, same day)
                            var isDuplicate = daySchedule.Items.Any(existing =>
                                string.Equals(existing.Title, animeItem.Title, StringComparison.OrdinalIgnoreCase) &&
                                existing.Episode == animeItem.Episode &&
                                Math.Abs((existing.AiringAt - animeItem.AiringAt).TotalMinutes) < 5);

                            if (!isDuplicate)
                            {
                                daySchedule.Items.Add(animeItem);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing schedule item: {ex.Message}");
                    // Continue processing other items
                }
            }

            // Sort items within each day by airing time
            foreach (var day in daySchedules)
            {
                var sortedItems = day.Items.OrderBy(item => item.AiringAt).ToList();
                day.Items.Clear();
                foreach (var item in sortedItems)
                {
                    day.Items.Add(item);
                }
            }

            return daySchedules;
        }

        private static AnimeScheduleItem? ParseAnimeScheduleItem(JToken schedule, HashSet<string> watchSet)
        {
            var media = schedule["media"];
            if (media == null) return null;

            var title = GetBestTitle(media["title"]);
            if (string.IsNullOrEmpty(title)) return null;

            if (!long.TryParse(schedule["airingAt"]?.ToString(), out var airingAtUnix))
                return null;

            var airingAt = DateTimeOffset.FromUnixTimeSeconds(airingAtUnix).LocalDateTime;
            var episode = schedule["episode"]?.ToObject<int>() ?? 0;

            var coverImage = media["coverImage"];
            var imageUrl = coverImage?["large"]?.ToString() ??
                          coverImage?["medium"]?.ToString() ??
                          "/api/placeholder/300/400";

            var format = media["format"]?.ToString() ?? "TV";
            var duration = media["duration"]?.ToObject<int>() ?? 24;
            var genres = media["genres"]?.ToObject<List<string>>() ?? new List<string>();
            var studio = media["studios"]?["nodes"]?.FirstOrDefault()?["name"]?.ToString() ?? "";
            var averageScore = media["averageScore"]?.ToObject<double>() ?? 0;
            var description = media["description"]?.ToString() ?? "";
            var status = media["status"]?.ToString() ?? "";

            if (!string.IsNullOrEmpty(description))
            {
                description = System.Text.RegularExpressions.Regex.Replace(description, "<.*?>", "");
                if (description.Length > 200)
                {
                    description = description.Substring(0, 200) + "...";
                }
            }

            return new AnimeScheduleItem
            {
                Title = title,
                ImageUrl = imageUrl,
                AiringAt = airingAt,
                Episode = episode,
                EpisodeInfo = episode > 0 ? $"EP{episode} • {format}" : format,
                Type = format,
                Duration = duration,
                Genre = string.Join(", ", genres.Take(2)),
                Studio = studio,
                Rating = averageScore / 10.0,
                Description = description,
                Status = status,
                IsBookmarked = watchSet.Contains(title)
            };
        }

        private static string GetBestTitle(JToken? titleObject)
        {
            if (titleObject == null) return "";

            return titleObject["english"]?.ToString() ??
                   titleObject["romaji"]?.ToString() ??
                   titleObject["native"]?.ToString() ??
                   "";
        }
    }

    public class AnimeSearchResult
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public string Format { get; set; } = "";
        public string Status { get; set; } = "";
        public int? Episodes { get; set; }
        public double Rating { get; set; }
        public List<string> Genres { get; set; } = new();
        public string Studio { get; set; } = "";
    }
}
