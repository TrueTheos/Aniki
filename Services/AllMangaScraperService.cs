using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Aniki.Services.Interfaces;

namespace Aniki.Services;

public class AllMangaScraperService : IAllMangaScraperService
{
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, string> _hashCache = new();

    private const string ALLANIME_BASE = "allanime.day";
    private const string ALLANIME_API  = "https://api.allanime.day";
    private const string ALLANIME_REFR = "https://allmanga.to";

    private const string SEARCH_GQL = "query($search: SearchInput $limit: Int $page: Int $translationType: VaildTranslationTypeEnumType $countryOrigin: VaildCountryOriginEnumType) { shows(search: $search limit: $limit page: $page translationType: $translationType countryOrigin: $countryOrigin) { edges { _id name availableEpisodes malId banner __typename } } }";
    private const string DETAILS_GQL = "query($showId: String!) { show(_id: $showId) { _id name malId aniListId description availableEpisodesDetail thumbnail __typename } }";
    private const string EPISODES_GQL = "query($showId: String!) { show(_id: $showId) { _id availableEpisodesDetail } }";
    private const string EPISODE_GQL = "query($showId: String!, $translationType: VaildTranslationTypeEnumType!, $episodeString: String!) { episode(showId: $showId translationType: $translationType episodeString: $episodeString) { episodeString sourceUrls } }";

    public AllMangaScraperService()
    {
        HttpClientHandler handler = new() { AllowAutoRedirect = true, UseCookies = true };

        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/145.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Referer",             ALLANIME_REFR + "/");
        _httpClient.DefaultRequestHeaders.Add("Origin",              ALLANIME_REFR);
        _httpClient.DefaultRequestHeaders.Add("sec-ch-ua",           "\"Not:A-Brand\";v=\"99\", \"Chromium\";v=\"145\"");
        _httpClient.DefaultRequestHeaders.Add("sec-ch-ua-mobile",    "?0");
        _httpClient.DefaultRequestHeaders.Add("sec-ch-ua-platform",  "\"Windows\"");
    }

    // ── APQ core ─────────────────────────────────────────────────────────────

    private string ComputeHash(string query)
    {
        if (_hashCache.TryGetValue(query, out string? cached))
            return cached;

        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(query));
        string hash  = Convert.ToHexString(bytes).ToLower();
        _hashCache[query] = hash;
        return hash;
    }

    private async Task<string> ApqGetAsync(string gql, object variables)
    {
        string hash = ComputeHash(gql);

        string extensionsJson = JsonSerializer.Serialize(new
        {
            persistedQuery = new { version = 1, sha256Hash = hash }
        });

        string variablesJson = JsonSerializer.Serialize(variables);

        string url = $"{ALLANIME_API}/api" +
                     $"?variables={HttpUtility.UrlEncode(variablesJson)}" +
                     $"&extensions={HttpUtility.UrlEncode(extensionsJson)}";

        string response = await _httpClient.GetStringAsync(url);

        // If hash not recognised by server, resend with full query to re-register it
        if (response.Contains("PersistedQueryNotFound"))
        {
            url += $"&query={HttpUtility.UrlEncode(gql)}";
            response = await _httpClient.GetStringAsync(url);
        }

        return response;
    }

    // ── public API ───────────────────────────────────────────────────────────

    public async Task<List<AllMangaSearchResult>> SearchAnimeAsync(string query)
    {
        try
        {
            var variables = new
            {
                search = new { allowAdult = false, allowUnknown = false, query },
                limit  = 40,
                page   = 1,
                translationType = "sub",
                countryOrigin   = "ALL"
            };

            string response = await ApqGetAsync(SEARCH_GQL, variables);
            using JsonDocument jsonDoc = JsonDocument.Parse(response);

            List<AllMangaSearchResult> results = new();

            if (jsonDoc.RootElement.TryGetProperty("data",  out JsonElement data)  &&
                data.TryGetProperty("shows",                out JsonElement shows)  &&
                shows.TryGetProperty("edges",               out JsonElement edges))
            {
                foreach (JsonElement edge in edges.EnumerateArray())
                {
                    string? id   = edge.GetProperty("_id").GetString();
                    string? name = edge.GetProperty("name").GetString();

                    int? malId = null;
                    if (edge.TryGetProperty("malId", out JsonElement malIdProp))
                    {
                        if (malIdProp.ValueKind == JsonValueKind.String &&
                            int.TryParse(malIdProp.GetString(), out int parsed))
                            malId = parsed;
                        else if (malIdProp.ValueKind == JsonValueKind.Number)
                            malId = malIdProp.GetInt32();
                    }

                    int episodeCount = 0;
                    if (edge.TryGetProperty("availableEpisodes", out JsonElement availableEpisodes) &&
                        availableEpisodes.TryGetProperty("sub",  out JsonElement subCount))
                        episodeCount = subCount.GetInt32();

                    string? banner = null;
                    if (edge.TryGetProperty("banner", out JsonElement bannerEl))
                        banner = bannerEl.GetString();

                    if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name))
                    {
                        results.Add(new AllMangaSearchResult
                        {
                            Id       = id,
                            Title    = name,
                            Episodes = episodeCount,
                            Url      = $"{ALLANIME_REFR}/anime/{id}",
                            MalId    = malId,
                            Banner   = banner
                        });
                    }
                }
            }

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

    public async Task<AllMangaAnimeDetails?> GetAnimeDetailsAsync(string animeIdOrUrl)
    {
        try
        {
            string showId    = ExtractShowId(animeIdOrUrl);
            var    variables = new { showId };

            string response = await ApqGetAsync(DETAILS_GQL, variables);
            using JsonDocument jsonDoc = JsonDocument.Parse(response);

            if (!jsonDoc.RootElement.TryGetProperty("data", out JsonElement data) ||
                !data.TryGetProperty("show", out JsonElement show))
                return null;

            AllMangaAnimeDetails details = new()
            {
                Id   = show.GetProperty("_id").GetString()   ?? string.Empty,
                Name = show.GetProperty("name").GetString()  ?? string.Empty
            };

            if (show.TryGetProperty("malId", out JsonElement malIdProp) &&
                malIdProp.ValueKind == JsonValueKind.Number)
                details.MalId = malIdProp.GetInt32();

            if (show.TryGetProperty("aniListId", out JsonElement aniListIdProp) &&
                aniListIdProp.ValueKind == JsonValueKind.Number)
                details.AniListId = aniListIdProp.GetInt32();

            if (show.TryGetProperty("description", out JsonElement descProp))
                details.Description = descProp.GetString();

            if (show.TryGetProperty("thumbnail", out JsonElement thumbProp))
                details.Thumbnail = thumbProp.GetString();

            return details;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to get anime details: {ex.Message}", ex);
        }
    }

    public async Task<List<AllMangaEpisode>> GetEpisodesAsync(string animeIdOrUrl)
    {
        try
        {
            string showId    = ExtractShowId(animeIdOrUrl);
            var    variables = new { showId };

            string response = await ApqGetAsync(EPISODES_GQL, variables);
            using JsonDocument jsonDoc = JsonDocument.Parse(response);

            List<AllMangaEpisode> episodes = new();

            if (jsonDoc.RootElement.TryGetProperty("data", out JsonElement data)    &&
                data.TryGetProperty("show",                out JsonElement show)     &&
                show.TryGetProperty("availableEpisodesDetail", out JsonElement detail) &&
                detail.TryGetProperty("sub",               out JsonElement subEps))
            {
                foreach (JsonElement ep in subEps.EnumerateArray())
                {
                    string? epString = ep.GetString();
                    if (float.TryParse(epString, out float epNum))
                    {
                        episodes.Add(new AllMangaEpisode
                        {
                            Id             = $"{showId}-{epString}",
                            Number         = (int)epNum,
                            Url            = $"{ALLANIME_REFR}/anime/{showId}/episodes/sub/{epString}",
                            ShowId         = showId,
                            EpisodeString  = epString,
                            TotalEpisodes  = subEps.GetArrayLength()
                        });
                    }
                }
            }

            return episodes.OrderBy(e => e.Number).ToList();
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to get episodes: {ex.Message}", ex);
        }
    }

    public async Task<string> GetVideoUrlAsync(string episodeUrl)
    {
        try
        {
            string[] parts        = episodeUrl.Split('/');
            string   showId       = parts[^4];
            string   episodeString = parts[^1];

            var variables = new { showId, translationType = "sub", episodeString };

            string response = await ApqGetAsync(EPISODE_GQL, variables);
            using JsonDocument jsonDoc = JsonDocument.Parse(response);

            if (jsonDoc.RootElement.TryGetProperty("data",    out JsonElement data)    &&
                data.TryGetProperty("episode",                 out JsonElement episode) &&
                episode.TryGetProperty("sourceUrls",           out JsonElement sourceUrls))
            {
                foreach (JsonElement source in sourceUrls.EnumerateArray())
                {
                    if (source.TryGetProperty("sourceUrl", out JsonElement sourceUrl))
                    {
                        string? encodedUrl = sourceUrl.GetString();
                        if (string.IsNullOrEmpty(encodedUrl)) continue;

                        string decodedUrl = DecodeSourceUrl(encodedUrl);
                        string videoLink  = await GetLinksFromSource(decodedUrl);
                        if (!string.IsNullOrEmpty(videoLink))
                            return videoLink;
                    }
                }
            }

            throw new Exception("No valid video sources found");
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to get video URL: {ex.Message}", ex);
        }
    }

    // ── private helpers ──────────────────────────────────────────────────────

    private static string ExtractShowId(string animeIdOrUrl) =>
        animeIdOrUrl.Contains('/') ? animeIdOrUrl.Split('/').Last() : animeIdOrUrl;

    private int CalculateAnimeScore(AllMangaSearchResult anime, string query)
    {
        if (DoesTitleMatch(anime, query)) return 1000;

        int score = FuzzySharp.Fuzz.TokenSortRatio(anime.Title, query);
        if (anime.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            score += 50;

        return score;
    }

    private bool DoesTitleMatch(AllMangaSearchResult anime, string query) =>
        NormalizeTitleToLower(anime.Title) == NormalizeTitleToLower(query);

    private static string NormalizeTitleToLower(string? title)
    {
        if (string.IsNullOrEmpty(title)) return string.Empty;
        string normalized = title.Replace("-", "").Replace("_", "").Replace(":", "").Trim();
        normalized = Regex.Replace(normalized, @"\s+", " ");
        return normalized.ToLower();
    }

    private string DecodeSourceUrl(string encoded)
    {
        if (string.IsNullOrEmpty(encoded) || !encoded.StartsWith("--"))
            return encoded;

        encoded = encoded[2..];
        StringBuilder decoded = new();

        for (int i = 0; i + 1 < encoded.Length; i += 2)
        {
            string chr = DecodeHexChar(encoded.Substring(i, 2));
            if (!string.IsNullOrEmpty(chr))
                decoded.Append(chr);
        }

        return decoded.ToString().Replace("/clock", "/clock.json");
    }

    private static string DecodeHexChar(string hex) => hex switch
    {
        "79" => "A", "7a" => "B", "7b" => "C", "7c" => "D", "7d" => "E", "7e" => "F", "7f" => "G",
        "70" => "H", "71" => "I", "72" => "J", "73" => "K", "74" => "L", "75" => "M", "76" => "N", "77" => "O",
        "68" => "P", "69" => "Q", "6a" => "R", "6b" => "S", "6c" => "T", "6d" => "U", "6e" => "V", "6f" => "W",
        "60" => "X", "61" => "Y", "62" => "Z",
        "59" => "a", "5a" => "b", "5b" => "c", "5c" => "d", "5d" => "e", "5e" => "f", "5f" => "g",
        "50" => "h", "51" => "i", "52" => "j", "53" => "k", "54" => "l", "55" => "m", "56" => "n", "57" => "o",
        "48" => "p", "49" => "q", "4a" => "r", "4b" => "s", "4c" => "t", "4d" => "u", "4e" => "v", "4f" => "w",
        "40" => "x", "41" => "y", "42" => "z",
        "08" => "0", "09" => "1", "0a" => "2", "0b" => "3", "0c" => "4", "0d" => "5", "0e" => "6", "0f" => "7",
        "00" => "8", "01" => "9",
        "15" => "-", "16" => ".", "67" => "_", "46" => "~", "02" => ":", "17" => "/", "07" => "?",
        "1b" => "#", "63" => "[", "65" => "]", "78" => "@", "19" => "!", "1c" => "$", "1e" => "&",
        "10" => "(", "11" => ")", "12" => "*", "13" => "+", "14" => ",", "03" => ";", "05" => "=", "1d" => "%",
        _ => ""
    };

    private async Task<string> GetLinksFromSource(string sourceUrl)
    {
        try
        {
            string response = await _httpClient.GetStringAsync($"https://{ALLANIME_BASE}{sourceUrl}");
            using JsonDocument jsonDoc = JsonDocument.Parse(response);

            if (jsonDoc.RootElement.TryGetProperty("links", out JsonElement links))
            {
                foreach (JsonElement link in links.EnumerateArray())
                {
                    if (link.TryGetProperty("link",          out JsonElement linkProp) &&
                        link.TryGetProperty("resolutionStr", out JsonElement _))
                    {
                        string? url = linkProp.GetString();
                        if (!string.IsNullOrEmpty(url))
                            return url;
                    }
                }
            }

            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}