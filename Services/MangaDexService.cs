using System.Globalization;
using System.Text.Json;
using Aniki.Services.Auth;

namespace Aniki.Services;

internal sealed record MangaChapter(string Id, string Number, string? Title, int Pages, string? ExternalUrl = null, string? Source = null)
{
    public double NumberValue => double.TryParse(Number, NumberStyles.Float, CultureInfo.InvariantCulture, out double n) ? n : 0;
    public bool IsExternal => ExternalUrl != null;
    public string Display => (string.IsNullOrWhiteSpace(Title) ? $"Chapter {Number}" : $"Chapter {Number} - {Title}")
                             + (IsExternal ? $"  ↗ {Source}" : "");
}

internal sealed class MangaDexService : IDisposable
{
    private const string API = "https://api.mangadex.org";
    private readonly HttpClient _client = new();

    public MangaDexService()
    {
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("Aniki");
    }

    private async Task<JsonDocument> GetJsonAsync(string url)
    {
        using HttpResponseMessage response = await _client.GetAsync(new Uri(url)).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonDocument.Parse(json);
    }

    public async Task<string?> FindMangaIdAsync(int providerId, ILoginProvider.ProviderType provider, string title)
    {
        string linkKey = provider == ILoginProvider.ProviderType.AniList ? "al" : "mal";
        string id = providerId.ToString(CultureInfo.InvariantCulture);

        using JsonDocument doc = await GetJsonAsync(
            $"{API}/manga?title={Uri.EscapeDataString(title)}&limit=10" +
            "&contentRating[]=safe&contentRating[]=suggestive&contentRating[]=erotica").ConfigureAwait(false);

        string? first = null;
        foreach (JsonElement manga in doc.RootElement.GetProperty("data").EnumerateArray())
        {
            string mangaId = manga.GetProperty("id").GetString()!;
            first ??= mangaId;
            if (manga.GetProperty("attributes").TryGetProperty("links", out JsonElement links) &&
                links.ValueKind == JsonValueKind.Object &&
                links.TryGetProperty(linkKey, out JsonElement link) &&
                link.GetString() == id)
                return mangaId;
        }
        return first;
    }

    public async Task<List<MangaChapter>> GetChaptersAsync(string mangaId)
    {
        Dictionary<string, MangaChapter> byNumber = new();
        int offset = 0, total;
        do
        {
            using JsonDocument doc = await GetJsonAsync(
                $"{API}/manga/{mangaId}/feed?translatedLanguage[]=en&order[chapter]=asc&limit=500&offset={offset}&includes[]=scanlation_group" +
                "&contentRating[]=safe&contentRating[]=suggestive&contentRating[]=erotica").ConfigureAwait(false);
            total = doc.RootElement.GetProperty("total").GetInt32();
            JsonElement data = doc.RootElement.GetProperty("data");
            if (data.GetArrayLength() == 0) break;
            offset += data.GetArrayLength();

            foreach (JsonElement chapter in data.EnumerateArray())
            {
                JsonElement a = chapter.GetProperty("attributes");
                string? externalUrl = a.GetProperty("externalUrl").GetString();
                int pages = a.GetProperty("pages").GetInt32();
                if (externalUrl == null && pages == 0) continue;

                string number = a.GetProperty("chapter").GetString() ?? "0";
                if (byNumber.TryGetValue(number, out MangaChapter? existing) && (!existing.IsExternal || externalUrl != null))
                    continue;

                byNumber[number] = new MangaChapter(chapter.GetProperty("id").GetString()!, number,
                    a.GetProperty("title").GetString(), pages, externalUrl,
                    externalUrl == null ? null : GroupName(chapter) ?? new Uri(externalUrl).Host);
            }
        } while (offset < total && offset < 10_000); 

        return byNumber.Values.OrderBy(c => c.NumberValue).ToList();
    }

    private static string? GroupName(JsonElement chapter) =>
        chapter.GetProperty("relationships").EnumerateArray()
               .Where(r => r.GetProperty("type").GetString() == "scanlation_group" && r.TryGetProperty("attributes", out _))
               .Select(r => r.GetProperty("attributes").GetProperty("name").GetString())
               .FirstOrDefault();

    public async Task<List<string>> GetPageUrlsAsync(string chapterId)
    {
        using JsonDocument doc = await GetJsonAsync($"{API}/at-home/server/{chapterId}").ConfigureAwait(false);
        string baseUrl = doc.RootElement.GetProperty("baseUrl").GetString()!;
        JsonElement chapter = doc.RootElement.GetProperty("chapter");
        string hash = chapter.GetProperty("hash").GetString()!;
        return chapter.GetProperty("data").EnumerateArray()
                      .Select(f => $"{baseUrl}/data/{hash}/{f.GetString()}")
                      .ToList();
    }

    public void Dispose() => _client.Dispose();
}
