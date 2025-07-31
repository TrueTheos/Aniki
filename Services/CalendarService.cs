using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Aniki.Models;

namespace Aniki.Services;

public static class CalendarService
{
    private const string GraphQlEndpoint = "https://graphql.anilist.co";

    private const string Query = @"
              query ($page: Int, $perPage: Int, $airingAt_greater: Int, $airingAt_lesser: Int) {
                Page(page: $page, perPage: $perPage) {
                  pageInfo {
                    hasNextPage
                  }
                  airingSchedules(
                    airingAt_greater: $airingAt_greater, 
                    airingAt_lesser: $airingAt_lesser,
                    sort: TIME
                  ) {
                    media { 
                      id
                      idMal
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

    private static readonly HttpClient Http = new();

    public static async Task<List<DaySchedule>> GetWeeklyScheduleAsync(
        IEnumerable<string> watchingList,
        int perPage = 150,
        DateTime? specificWeek = null)
    {
        DateTime referenceDate = specificWeek ?? DateTime.Now;
        DateTime startOfWeek = referenceDate;
        DateTime endOfWeek = startOfWeek.AddDays(7);

        long startUnix = ((DateTimeOffset)startOfWeek).ToUnixTimeSeconds();
        long endUnix = ((DateTimeOffset)endOfWeek).ToUnixTimeSeconds();

        JArray schedules = new JArray();
        int currentPage = 1;
        bool hasNextPage;

        do
        {
            JObject payload = new JObject
            {
                ["query"] = Query,
                ["variables"] = new JObject
                {
                    ["page"] = currentPage,
                    ["perPage"] = perPage,
                    ["airingAt_greater"] = startUnix,
                    ["airingAt_lesser"] = endUnix
                }
            };

            StringContent content = new(payload.ToString(), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await Http.PostAsync(GraphQlEndpoint, content);
            response.EnsureSuccessStatusCode();

            string body = await response.Content.ReadAsStringAsync();
            JObject json = JObject.Parse(body);

            JToken? pageNode = json["data"]?["Page"];
            if (pageNode == null) break;

            if (pageNode["airingSchedules"] is JArray pageSchedules)
            {
                foreach (JToken schedule in pageSchedules)
                {
                    schedules.Add(schedule);
                }
            }

            hasNextPage = pageNode["pageInfo"]?["hasNextPage"]?.Value<bool>() ?? false;
            currentPage++;

        } while (hasNextPage);

        HashSet<string> watchSet = new(watchingList, StringComparer.OrdinalIgnoreCase);

        List<DaySchedule> daySchedules = [];
        for (int i = 0; i < 7; i++)
        {
            DateTime currentDate = startOfWeek.AddDays(i);
            daySchedules.Add(new()
            {
                Name = currentDate.DayOfWeek.ToString(),
                DayName = currentDate.ToString("dddd"),
                Date = currentDate,
                IsToday = currentDate.Date == DateTime.Today,
                Items = []
            });
        }

        Dictionary<string, DaySchedule> byDay = daySchedules.ToDictionary(ds => ds.Name, StringComparer.OrdinalIgnoreCase);

        foreach (JToken schedule in schedules)
        {
            try
            {
                AnimeScheduleItem? animeItem = ParseAnimeScheduleItem(schedule, watchSet);
                if (animeItem == null) continue;
                string weekday = animeItem.AiringAt.ToString("dddd", CultureInfo.InvariantCulture);

                if (!byDay.TryGetValue(weekday, out DaySchedule? daySchedule)) continue;
                
                bool isDuplicate = daySchedule.Items.Any(existing =>
                    string.Equals(existing.Title, animeItem.Title, StringComparison.OrdinalIgnoreCase) &&
                    existing.Episode == animeItem.Episode &&
                    Math.Abs((existing.AiringAt - animeItem.AiringAt).TotalMinutes) < 5);

                if (!isDuplicate)
                {
                    daySchedule.Items.Add(animeItem);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing schedule item: {ex.Message}");
            }
        }

        foreach (DaySchedule day in daySchedules)
        {
            List<AnimeScheduleItem> sortedItems = day.Items.OrderBy(item => item.AiringAt).ToList();
            day.Items.Clear();
            foreach (AnimeScheduleItem item in sortedItems)
            {
                day.Items.Add(item);
            }
        }

        return daySchedules;
    }

    private static AnimeScheduleItem? ParseAnimeScheduleItem(JToken schedule, HashSet<string> watchSet)
    {
        JToken? media = schedule["media"];
        if (media == null) return null;

        string title = GetBestTitle(media["title"]);
        if (string.IsNullOrEmpty(title)) return null;

        if (!long.TryParse(schedule["airingAt"]?.ToString(), out long airingAtUnix))
            return null;

        DateTime airingAt = DateTimeOffset.FromUnixTimeSeconds(airingAtUnix).LocalDateTime;
        int episode = schedule["episode"]?.ToObject<int>() ?? 0;

        JToken? coverImage = media["coverImage"];
        string imageUrl = coverImage?["large"]?.ToString() ??
                          coverImage?["medium"]?.ToString() ??
                          "/api/placeholder/300/400";

        string format = media["format"]?.ToString() ?? "TV";
        int duration = media["duration"]?.ToObject<int>() ?? 24;
        List<string> genres = media["genres"]?.ToObject<List<string>>() ?? new List<string>();
        string studio = media["studios"]?["nodes"]?.FirstOrDefault()?["name"]?.ToString() ?? "";
        double averageScore = media["averageScore"]?.ToObject<double>() ?? 0;
        string description = media["description"]?.ToString() ?? "";
        string status = media["status"]?.ToString() ?? "";

        int? malId = media["idMal"]?.ToObject<int?>();

        if (!string.IsNullOrEmpty(description))
        {
            description = System.Text.RegularExpressions.Regex.Replace(description, "<.*?>", "");
            if (description.Length > 200)
            {
                description = description.Substring(0, 200) + "...";
            }
        }

        return new()
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
            MalId = malId,
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