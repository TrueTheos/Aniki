using System.Text.Json;
using Aniki.Services.Interfaces;

namespace Aniki.Services.Anime.Providers;

public class JikanService : IJikanService
{
    private const int RATE_LIMIT_PER_SECOND = 3;
    private const int RATE_LIMIT_WINDOW_MS  = 1000;

    private readonly HttpClient _client = new();
    private readonly SemaphoreSlim _rateLimitLock = new(1, 1);
    private readonly Queue<DateTime> _requestTimestamps = new();

    private readonly Dictionary<int, string?> _trailerUrlCache = new();

    private async Task<HttpResponseMessage> GetAsync(string url)
    {
        await _rateLimitLock.WaitAsync();
        try
        {
            DateTime now = DateTime.UtcNow;

            while (_requestTimestamps.Count > 0 &&
                   (now - _requestTimestamps.Peek()).TotalMilliseconds >= RATE_LIMIT_WINDOW_MS)
                _requestTimestamps.Dequeue();

            if (_requestTimestamps.Count >= RATE_LIMIT_PER_SECOND)
            {
                double timePassed = (now - _requestTimestamps.Peek()).TotalMilliseconds;
                int wait = (int)(RATE_LIMIT_WINDOW_MS - timePassed) + 20;
                if (wait > 0) await Task.Delay(wait);

                while (_requestTimestamps.Count > 0 &&
                       (DateTime.UtcNow - _requestTimestamps.Peek()).TotalMilliseconds >= RATE_LIMIT_WINDOW_MS)
                    _requestTimestamps.Dequeue();
            }

            _requestTimestamps.Enqueue(DateTime.UtcNow);
        }
        finally
        {
            _rateLimitLock.Release();
        }

        return await _client.GetAsync(url);
    }

    public async Task<string?> GetAnimeTrailerUrlAsync(int malId)
    {
        if (_trailerUrlCache.TryGetValue(malId, out string? cached))
            return cached;

        try
        {
            HttpResponseMessage response = await GetAsync($"https://api.jikan.moe/v4/anime/{malId}/videos");

            if (!response.IsSuccessStatusCode)
                return null;

            string json = await response.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(json);

            string? url = null;
            if (doc.RootElement.TryGetProperty("data", out JsonElement data) &&
                data.TryGetProperty("promo", out JsonElement promo) &&
                promo.ValueKind == JsonValueKind.Array &&
                promo.GetArrayLength() > 0)
            {
                JsonElement first = promo[0];
                if (first.TryGetProperty("trailer", out JsonElement trailer))
                {
                    if (trailer.TryGetProperty("embed_url", out JsonElement embedUrl))
                        url = embedUrl.GetString();
                    else if (trailer.TryGetProperty("youtube_id", out JsonElement ytId))
                    {
                        string id = ytId.GetString() ?? "";
                        if (!string.IsNullOrEmpty(id))
                            url = $"https://www.youtube.com/watch?v={id}";
                    }
                }
            }

            _trailerUrlCache[malId] = url;
            return url;
        }
        catch
        {
            return null;
        }
    }
}
