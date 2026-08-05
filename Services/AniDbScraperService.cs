using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aniki.Services.Interfaces;

namespace Aniki.Services;

internal sealed class AniDbScraperService : IAniDbScraperService, IDisposable
{
    private readonly HttpClient _httpClient;

    private const string BASE = "https://anidb.app";
    private const string USER_AGENT =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/145.0.0.0 Safari/537.36";

    private static readonly Regex SearchCardRegex = new(
        @"anime/([a-z0-9-]+-\d+)""[^>]*title=""([^""]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PosterRegex = new(
        @"cdn\.xlsbox\.com/poster/[^""']+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex MalIdRegex = new(
        @"myanimelist\.net/anime/(\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex OgImageRegex = new(
        @"property=[""']og:image[""']\s+content=[""']([^""']+)[""']|content=[""']([^""']+)[""']\s+property=[""']og:image[""']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex EmbedFileRegex = new(
        @"file:\s*'([^']+)'",
        RegexOptions.Compiled);

    public AniDbScraperService()
    {
#pragma warning disable CA2000
        HttpClientHandler handler = new()
        {
            AllowAutoRedirect = true,
            UseCookies = true,
            CheckCertificateRevocationList = true
        };
#pragma warning restore CA2000

        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", USER_AGENT);
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Referer", BASE + "/");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Origin", BASE);
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json,text/html,*/*");
    }

    public async Task<List<AniDbSearchResult>> SearchAnimeAsync(string query)
    {
        try
        {
            string url = $"{BASE}/browse?q={Uri.EscapeDataString(query)}";
            string html = await _httpClient.GetStringAsync(url).ConfigureAwait(false);

            List<AniDbSearchResult> results = new();
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

            foreach (Match match in SearchCardRegex.Matches(html))
            {
                string slug = match.Groups[1].Value;
                if (!seen.Add(slug))
                    continue;

                string title = WebUtility.HtmlDecode(match.Groups[2].Value);
                string animeUrl = $"{BASE}/anime/{slug}";

                string? banner = null;
                int cardStart = Math.Max(0, match.Index - 50);
                int cardEnd = Math.Min(html.Length, match.Index + 800);
                Match poster = PosterRegex.Match(html, cardStart, cardEnd - cardStart);
                if (poster.Success)
                    banner = "https://" + poster.Value;

                results.Add(new AniDbSearchResult
                {
                    Id = ExtractNumericId(slug),
                    Title = title,
                    Url = animeUrl,
                    Banner = banner,
                    Episodes = 0,
                    MalId = null
                });
            }

            await EnrichMalIdsAsync(results).ConfigureAwait(false);

            return results
                .Select(x => new { Item = x, Score = CalculateAnimeScore(x, query) })
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Item.Episodes)
                .Select(x => x.Item)
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return new();
        }
    }

    public async Task<List<AniDbEpisode>> GetEpisodesAsync(string animeIdOrUrl)
    {
        try
        {
            string showId = ExtractNumericId(animeIdOrUrl);
            if (string.IsNullOrEmpty(showId))
                throw new InvalidOperationException("Could not resolve anime id");

            string response = await _httpClient
                .GetStringAsync($"{BASE}/api/frontend/anime/{showId}/episodes")
                .ConfigureAwait(false);

            using JsonDocument jsonDoc = JsonDocument.Parse(response);
            List<AniDbEpisode> episodes = new();

            if (!jsonDoc.RootElement.TryGetProperty("episodes", out JsonElement eps) ||
                eps.ValueKind != JsonValueKind.Array)
                return episodes;

            foreach (JsonElement ep in eps.EnumerateArray())
            {
                if (!ep.TryGetProperty("id", out JsonElement idEl) ||
                    !ep.TryGetProperty("number", out JsonElement numEl))
                    continue;

                int episodeId = idEl.GetInt32();
                int number = numEl.ValueKind == JsonValueKind.Number
                    ? numEl.GetInt32()
                    : int.TryParse(numEl.GetString(), out int parsed) ? parsed : 0;

                if (number <= 0)
                    continue;

                episodes.Add(new AniDbEpisode
                {
                    Id = episodeId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Number = number,
                    Url = episodeId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ShowId = showId,
                    EpisodeString = number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    TotalEpisodes = 0
                });
            }

            episodes = episodes.OrderBy(e => e.Number).ToList();
            foreach (AniDbEpisode episode in episodes)
                episode.TotalEpisodes = episodes.Count;

            return episodes;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get episodes: {ex.Message}", ex);
        }
    }

    public async Task<string> GetVideoUrlAsync(string episodeUrl)
    {
        try
        {
            string episodeId = ExtractEpisodeId(episodeUrl);
            if (string.IsNullOrEmpty(episodeId))
                throw new InvalidOperationException("Could not resolve episode id");

            string languagesJson = await _httpClient
                .GetStringAsync($"{BASE}/api/frontend/episode/{episodeId}/languages")
                .ConfigureAwait(false);

            string? embedUrl = PickEmbedUrl(languagesJson);
            if (string.IsNullOrEmpty(embedUrl))
                throw new InvalidOperationException("No embed URL for episode");

            string embedHtml = await _httpClient.GetStringAsync(embedUrl).ConfigureAwait(false);
            Match fileMatch = EmbedFileRegex.Match(embedHtml);
            if (!fileMatch.Success)
                throw new InvalidOperationException("No stream file found in embed");

            string masterUrl = fileMatch.Groups[1].Value;
            string best = await ResolveBestPlaylistAsync(masterUrl).ConfigureAwait(false);
            return string.IsNullOrEmpty(best) ? masterUrl : best;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get video URL: {ex.Message}", ex);
        }
    }

    private async Task EnrichMalIdsAsync(List<AniDbSearchResult> results)
    {
        if (results.Count == 0)
            return;

        using SemaphoreSlim gate = new(6);
        await Task.WhenAll(results.Select(async result =>
        {
            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                string html = await _httpClient.GetStringAsync(result.Url).ConfigureAwait(false);
                Match mal = MalIdRegex.Match(html);
                if (mal.Success && int.TryParse(mal.Groups[1].Value, out int malId))
                    result.MalId = malId;

                if (string.IsNullOrEmpty(result.Banner))
                {
                    Match og = OgImageRegex.Match(html);
                    if (og.Success)
                        result.Banner = og.Groups[1].Success ? og.Groups[1].Value : og.Groups[2].Value;
                }
            }
            catch
            {
                // MAL enrichment is best-effort
            }
            finally
            {
                gate.Release();
            }
        })).ConfigureAwait(false);
    }

    private static string? PickEmbedUrl(string languagesJson)
    {
        using JsonDocument doc = JsonDocument.Parse(languagesJson);
        if (!doc.RootElement.TryGetProperty("languages", out JsonElement languages) ||
            languages.ValueKind != JsonValueKind.Array)
            return null;

        string? jpn = null;
        string? eng = null;
        string? any = null;

        foreach (JsonElement lang in languages.EnumerateArray())
        {
            if (!lang.TryGetProperty("embed_url", out JsonElement embedEl))
                continue;

            string? embed = embedEl.GetString();
            if (string.IsNullOrEmpty(embed))
                continue;

            embed = embed.Replace("\\/", "/", StringComparison.Ordinal);
            any ??= embed;

            string? code = lang.TryGetProperty("code", out JsonElement codeEl)
                ? codeEl.GetString()
                : null;

            if (string.Equals(code, "jpn", StringComparison.OrdinalIgnoreCase))
                jpn = embed;
            else if (string.Equals(code, "eng", StringComparison.OrdinalIgnoreCase))
                eng = embed;
        }

        return jpn ?? eng ?? any;
    }

    private async Task<string> ResolveBestPlaylistAsync(string masterUrl)
    {
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, masterUrl);
            request.Headers.TryAddWithoutValidation("Referer", BASE + "/");
            request.Headers.TryAddWithoutValidation("Origin", BASE);

            using HttpResponseMessage response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            string playlist = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!playlist.Contains("#EXTM3U", StringComparison.Ordinal))
                return masterUrl;

            Uri baseUri = new(masterUrl);
            string? bestUrl = null;
            int bestBandwidth = -1;
            string? lastInf = null;

            foreach (string rawLine in playlist.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.StartsWith("#EXT-X-I-FRAME", StringComparison.OrdinalIgnoreCase))
                {
                    lastInf = null;
                    continue;
                }

                if (line.StartsWith("#EXT-X-STREAM-INF", StringComparison.OrdinalIgnoreCase))
                {
                    lastInf = line;
                    continue;
                }

                if (lastInf == null || line.StartsWith('#') || string.IsNullOrEmpty(line))
                    continue;

                string streamUrl = Uri.TryCreate(baseUri, line, out Uri? resolved)
                    ? resolved.ToString()
                    : line;

                int bandwidth = 0;
                Match bwMatch = Regex.Match(lastInf, @"BANDWIDTH=(\d+)", RegexOptions.IgnoreCase);
                if (bwMatch.Success)
                    _ = int.TryParse(bwMatch.Groups[1].Value, out bandwidth);

                if (bandwidth >= bestBandwidth)
                {
                    bestBandwidth = bandwidth;
                    bestUrl = streamUrl;
                }

                lastInf = null;
            }

            return bestUrl ?? masterUrl;
        }
        catch
        {
            return masterUrl;
        }
    }

    private static string ExtractNumericId(string animeIdOrUrl)
    {
        if (string.IsNullOrWhiteSpace(animeIdOrUrl))
            return string.Empty;

        if (int.TryParse(animeIdOrUrl, out _))
            return animeIdOrUrl;

        string last = animeIdOrUrl.TrimEnd('/').Split('/').Last();
        int dash = last.LastIndexOf('-');
        if (dash >= 0 && dash < last.Length - 1)
        {
            string tail = last[(dash + 1)..];
            if (int.TryParse(tail, out _))
                return tail;
        }

        return int.TryParse(last, out _) ? last : string.Empty;
    }

    private static string ExtractEpisodeId(string episodeUrl)
    {
        if (string.IsNullOrWhiteSpace(episodeUrl))
            return string.Empty;

        if (int.TryParse(episodeUrl, out _))
            return episodeUrl;

        string last = episodeUrl.TrimEnd('/').Split('/').Last();
        return int.TryParse(last, out _) ? last : string.Empty;
    }

    private static int CalculateAnimeScore(AniDbSearchResult anime, string query)
    {
        if (DoesTitleMatch(anime, query))
            return 1000;

        int score = FuzzySharp.Fuzz.TokenSortRatio(anime.Title, query);
        if (anime.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            score += 50;

        return score;
    }

    private static bool DoesTitleMatch(AniDbSearchResult anime, string query) =>
        NormalizeTitleToLower(anime.Title) == NormalizeTitleToLower(query);

    private static string NormalizeTitleToLower(string? title)
    {
        if (string.IsNullOrEmpty(title))
            return string.Empty;

        string normalized = title
            .Replace("-", "", StringComparison.InvariantCulture)
            .Replace("_", "", StringComparison.InvariantCulture)
            .Replace(":", "", StringComparison.InvariantCulture)
            .Trim();
        normalized = Regex.Replace(normalized, @"\s+", " ");
        return normalized.ToLowerInvariant();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
