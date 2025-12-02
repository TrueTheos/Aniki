using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Text;
using Aniki.Services.Auth;
using Aniki.Services.Interfaces;
using Avalonia.Media.Imaging;

namespace Aniki.Services;

public class CalendarService : ICalendarService
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
                      type
                      meanScore
                    }
                    episode
                    airingAt
                    timeUntilAiring
                  }
                }
              }
            ";

    private readonly HttpClient _client = new();

    private  async Task<JArray> FetchAiringSchedulesAsync(long startUnix, long endUnix, int perPage = 50)
    {
        JArray schedules = new();
        int currentPage = 1;
        bool hasNextPage;

        do
        {
            JObject payload = new()
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
            HttpResponseMessage response = await _client.PostAsync(GraphQlEndpoint, content);
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

        return schedules;
    }

    public async Task<List<DaySchedule>> GetScheduleAsync(
        IEnumerable<string> watchingList,
        DateTime startDate,
        DateTime endDate,
        int perPage = 150)
    {
        long startUnix = ((DateTimeOffset)startDate).ToUnixTimeSeconds();
        long endUnix = ((DateTimeOffset)endDate).ToUnixTimeSeconds();

        JArray schedules = await FetchAiringSchedulesAsync(startUnix, endUnix, perPage);

        HashSet<string> watchSet = new(watchingList, StringComparer.OrdinalIgnoreCase);

        List<DaySchedule> daySchedules = [];
        for (DateTime date = startDate.Date; date < endDate.Date; date = date.AddDays(1))
        {
            daySchedules.Add(new()
            {
#pragma warning disable CS0618 // Type or member is obsolete
                Name = date.DayOfWeek.ToString(CultureInfo.InvariantCulture),
#pragma warning restore CS0618 // Type or member is obsolete
                DayName = date.ToString("dddd", CultureInfo.InvariantCulture),
                Date = date,
                IsToday = date.Date == DateTime.Today,
                Items = []
            });
        }

        Dictionary<string, DaySchedule> byDay = daySchedules.ToDictionary(ds => ds.Name, StringComparer.OrdinalIgnoreCase);

        foreach (JToken schedule in schedules)
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

    public async Task<List<AnimeScheduleItem>> GetAnimeScheduleForDayAsync(DateTime date)
    {
        long startUnix = ((DateTimeOffset)date.Date).ToUnixTimeSeconds();
        long endUnix = ((DateTimeOffset)date.Date.AddDays(1)).ToUnixTimeSeconds();

        JArray schedules = await FetchAiringSchedulesAsync(startUnix, endUnix);

        List<AnimeScheduleItem> animeItems = new();

        foreach (JToken schedule in schedules)
        {
            AnimeScheduleItem? animeItem = ParseAnimeScheduleItem(schedule, new());
            if (animeItem != null)
            {
                animeItems.Add(animeItem);
            }
        }

        return animeItems;
    }

    private AnimeScheduleItem? ParseAnimeScheduleItem(JToken schedule, HashSet<string> watchSet)
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
        int? aniListId = media["id"]?.ToObject<int?>();
        int? malId = media["idMal"]?.ToObject<int?>();
        
        int? meanScore = media["meanScore"]?.ToObject<int?>();

        float? meanScoreScaled = meanScore.HasValue
            ? meanScore.Value / 10f
            : null;
        
        AnimeScheduleItem result = new()
        {
            Title = title,
            AiringAt = airingAt,
            Episode = episode,
            EpisodeInfo = episode > 0 ? $"EP{episode} • {format}" : format,
            Type = format,
            IsBookmarked = watchSet.Contains(title),
            ImageUrl = imageUrl,
            Mean = meanScoreScaled ?? 0
        };

        if (aniListId.HasValue) result.ProviderId[ILoginProvider.ProviderType.AniList] = aniListId.Value;
        if (malId.HasValue) result.ProviderId[ILoginProvider.ProviderType.MAL] = malId.Value;

        return result;
    }

    private  string GetBestTitle(JToken? titleObject)
    {
        if (titleObject == null) return "";

        return titleObject["romaji"]?.ToString() ??
               titleObject["english"]?.ToString() ??
               titleObject["native"]?.ToString() ??
               "";
    }
    
    private async Task<Bitmap?> DownloadImageAsync(string url)
    {
        try
        {
            byte[] imageData = await _client.GetByteArrayAsync(url);
            using MemoryStream ms = new(imageData);
            return new Bitmap(ms);
        }
        catch
        {
            return null;
        }
    }
}