using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aniki.Services.Interfaces;

namespace Aniki.Services;

internal sealed partial class HiAnimeScraperService : IAniDbScraperService, IDisposable
{
    private const string BASE = "https://hianime.at";
    private const string USER_AGENT =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";
    private const string PREFERRED_SERVER = "ZokoAnime";
    private const int ENRICHED_RESULT_COUNT = 8;
    private static readonly byte[] BlobKey = "otaku-embed-v1"u8.ToArray();

    private readonly HttpClient _httpClient;

    public string StreamReferer { get; private set; } = "https://zokoanime.video/";

    [GeneratedRegex(@"<h3 class=""film-name"">\s*<a href=""[^""]*/([^""/]+)""\s*title=""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex CardNameRegex();

    [GeneratedRegex(@"<img[^>]*?(?:data-src|src)=""([^""]+)""[^>]*class=""film-poster-img""", RegexOptions.IgnoreCase)]
    private static partial Regex CardPosterRegex();

    [GeneratedRegex(@"tick-(?:eps|sub)""[^>]*>(?:<i[^>]*></i>)?\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex CardEpisodesRegex();

    [GeneratedRegex(@"data-number=""(\d+)""[^>]*?data-id=""(\d+)""", RegexOptions.IgnoreCase)]
    private static partial Regex EpisodeRegex();

    [GeneratedRegex(@"data-type=""(sub|dub)""[^>]*?data-server-name=""([^""]+)""[^>]*?data-hash=""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex ServerRegex();

    [GeneratedRegex(@"window\.__P=""([^""]*)""")]
    private static partial Regex EmbedBlobRegex();

    [GeneratedRegex(@"/mal/(\d+)/")]
    private static partial Regex MalIdRegex();

    public HiAnimeScraperService()
    {
#pragma warning disable CA2000
        HttpClientHandler handler = new()
        {
            AllowAutoRedirect = true,
            UseCookies = true,
            CheckCertificateRevocationList = true
        };
#pragma warning restore CA2000

        _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", USER_AGENT);
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/json,*/*");
    }

    public async Task<List<AniDbSearchResult>> SearchAnimeAsync(string query)
    {
        try
        {
            string html = await _httpClient.GetStringAsync($"{BASE}/search?keyword={Uri.EscapeDataString(query)}").ConfigureAwait(false);

            int sidebar = html.IndexOf("id=\"main-sidebar\"", StringComparison.Ordinal);
            if (sidebar > 0) html = html[..sidebar];

            List<AniDbSearchResult> results = [];
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

            foreach (string card in html.Split("<div class=\"flw-item", StringSplitOptions.RemoveEmptyEntries).Skip(1))
            {
                Match name = CardNameRegex().Match(card);
                if (!name.Success) continue;

                string slug = name.Groups[1].Value;
                if (!seen.Add(slug)) continue;

                Match poster = CardPosterRegex().Match(card);
                Match eps = CardEpisodesRegex().Match(card);

                results.Add(new AniDbSearchResult
                {
                    Id = ExtractNumericId(slug),
                    Title = WebUtility.HtmlDecode(name.Groups[2].Value),
                    Url = $"{BASE}/{slug}",
                    Banner = poster.Success ? poster.Groups[1].Value : null,
                    Episodes = eps.Success ? int.Parse(eps.Groups[1].Value, CultureInfo.InvariantCulture) : 0,
                    MalId = null
                });
            }

            List<AniDbSearchResult> sorted = results
                .OrderByDescending(x => CalculateAnimeScore(x, query))
                .ThenByDescending(x => x.Episodes)
                .ToList();

            await EnrichMalIdsAsync(sorted.Take(ENRICHED_RESULT_COUNT).ToList()).ConfigureAwait(false);
            return sorted;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return [];
        }
    }

    public async Task<List<AniDbEpisode>> GetEpisodesAsync(string animeIdOrUrl)
    {
        try
        {
            string showId = ExtractNumericId(animeIdOrUrl);
            if (string.IsNullOrEmpty(showId))
                throw new InvalidOperationException("Could not resolve anime id");

            string html = await GetHtmlFieldAsync($"{BASE}/api/theme/episode/list/{showId}").ConfigureAwait(false);

            List<AniDbEpisode> episodes = [];
            foreach (Match m in EpisodeRegex().Matches(html))
            {
                int number = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                string id = m.Groups[2].Value;
                if (number <= 0) continue;

                episodes.Add(new AniDbEpisode
                {
                    Id = id,
                    Number = number,
                    Url = id,
                    ShowId = showId,
                    EpisodeString = m.Groups[1].Value,
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

            string? embedUrl = await GetEmbedUrlAsync(episodeId).ConfigureAwait(false);
            if (string.IsNullOrEmpty(embedUrl))
                throw new InvalidOperationException("No supported server for episode");

            Uri embedUri = new(embedUrl);
            StreamReferer = $"{embedUri.Scheme}://{embedUri.Host}/";

            string embedHtml = await _httpClient.GetStringAsync(embedUrl).ConfigureAwait(false);
            Match blob = EmbedBlobRegex().Match(embedHtml);
            if (!blob.Success)
                throw new InvalidOperationException("No player config in embed");

            string json = DeobfuscateBlob(blob.Groups[1].Value);
            using JsonDocument doc = JsonDocument.Parse(json);
            string? masterUrl = doc.RootElement.TryGetProperty("src", out JsonElement src) ? src.GetString() : null;
            if (string.IsNullOrEmpty(masterUrl))
                throw new InvalidOperationException("No stream in player config");

            string best = await ResolveBestPlaylistAsync(masterUrl).ConfigureAwait(false);
            string url = string.IsNullOrEmpty(best) ? masterUrl : best;
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                throw new InvalidOperationException("Stream URL is not http(s)");
            return url;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get video URL: {ex.Message}", ex);
        }
    }

    private async Task<string?> GetEmbedUrlAsync(string episodeId)
    {
        string html = await GetHtmlFieldAsync($"{BASE}/api/theme/episode/servers?episodeId={episodeId}").ConfigureAwait(false);

        string? sub = null, dub = null;
        foreach (Match m in ServerRegex().Matches(html))
        {
            if (!m.Groups[2].Value.Equals(PREFERRED_SERVER, StringComparison.OrdinalIgnoreCase)) continue;

            string embed = Encoding.UTF8.GetString(Convert.FromBase64String(m.Groups[3].Value));
            if (m.Groups[1].Value.Equals("sub", StringComparison.OrdinalIgnoreCase)) sub ??= embed;
            else dub ??= embed;
        }

        return sub ?? dub;
    }

    private async Task EnrichMalIdsAsync(List<AniDbSearchResult> results)
    {
        if (results.Count == 0) return;

        using SemaphoreSlim gate = new(4);
        await Task.WhenAll(results.Select(async result =>
        {
            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                string episodes = await GetHtmlFieldAsync($"{BASE}/api/theme/episode/list/{result.Id}").ConfigureAwait(false);
                Match first = EpisodeRegex().Match(episodes);
                if (!first.Success) return;

                string? embed = await GetEmbedUrlAsync(first.Groups[2].Value).ConfigureAwait(false);
                Match mal = embed == null ? Match.Empty : MalIdRegex().Match(embed);
                if (mal.Success && int.TryParse(mal.Groups[1].Value, out int malId))
                    result.MalId = malId;
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

    private async Task<string> GetHtmlFieldAsync(string url)
    {
        string json = await _httpClient.GetStringAsync(url).ConfigureAwait(false);
        using JsonDocument doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("html", out JsonElement html) ? html.GetString() ?? "" : "";
    }

    private static string DeobfuscateBlob(string base64)
    {
        byte[] bytes = Convert.FromBase64String(base64);
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] ^= BlobKey[i % BlobKey.Length];
        return Encoding.UTF8.GetString(bytes);
    }

    private async Task<string> ResolveBestPlaylistAsync(string masterUrl)
    {
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, masterUrl);
            request.Headers.TryAddWithoutValidation("Referer", StreamReferer);
            request.Headers.TryAddWithoutValidation("Origin", StreamReferer.TrimEnd('/'));

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

                string streamUrl = Uri.TryCreate(baseUri, line, out Uri? resolved) ? resolved.ToString() : line;

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
