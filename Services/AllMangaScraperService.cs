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

    private const string SEARCH_GQL  = "query($search: SearchInput $limit: Int $page: Int $translationType: VaildTranslationTypeEnumType $countryOrigin: VaildCountryOriginEnumType) { shows(search: $search limit: $limit page: $page translationType: $translationType countryOrigin: $countryOrigin) { edges { _id name availableEpisodes malId banner __typename } } }";
    private const string DETAILS_GQL = "query($showId: String!) { show(_id: $showId) { _id name malId aniListId description availableEpisodesDetail thumbnail __typename } }";
    private const string EPISODES_GQL = "query($showId: String!) { show(_id: $showId) { _id availableEpisodesDetail } }";
    private const string EPISODE_SOURCES_POST_GQL = "query ($showId: String!, $translationType: VaildTranslationTypeEnumType!, $episodeString: String!) { episode( showId: $showId translationType: $translationType episodeString: $episodeString ) { episodeString sourceUrls }}";

    // Registered on AllAnime's server for episode source requests (ani-cli / anilix)
    private const string EPISODE_SOURCES_PERSISTED_HASH = "d405d0edd690624b66baba3068e0edc3ac90f1597d898a1ec8db4e5c43c00fec";

    //Hardcoded in the site bundle
    private const string TOBEPARSED_KEY_MATERIAL = "Xot36i3lK3:v1";

    private static readonly Regex Mp4UploadSrcRegex = new(@"src:\s*""([^""]+)""", RegexOptions.Compiled);
    private static readonly Regex ClockRefererRegex = new(@"""Referer""\s*:\s*""([^""]+)""", RegexOptions.Compiled);
    private static readonly Regex WixmpUrlsetRegex = new(@"/urlset/[^/]+/(\d+),", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public AllMangaScraperService()
    {
        HttpClientHandler handler = new() { AllowAutoRedirect = true, UseCookies = true };

        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/145.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Referer",            ALLANIME_REFR + "/");
        _httpClient.DefaultRequestHeaders.Add("Origin",             ALLANIME_REFR);
        _httpClient.DefaultRequestHeaders.Add("sec-ch-ua",          "\"Not:A-Brand\";v=\"99\", \"Chromium\";v=\"145\"");
        _httpClient.DefaultRequestHeaders.Add("sec-ch-ua-mobile",   "?0");
        _httpClient.DefaultRequestHeaders.Add("sec-ch-ua-platform", "\"Windows\"");
    }

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

        if (response.Contains("PersistedQueryNotFound"))
        {
            url += $"&query={HttpUtility.UrlEncode(gql)}";
            response = await _httpClient.GetStringAsync(url);
        }

        return response;
    }

    private async Task<string> ApqGetEpisodeSourcesAsync(object variables)
    {
        string variablesJson = JsonSerializer.Serialize(variables);
        string extensionsJson = JsonSerializer.Serialize(new
        {
            persistedQuery = new { version = 1, sha256Hash = EPISODE_SOURCES_PERSISTED_HASH }
        });

        string url = $"{ALLANIME_API}/api" +
                     $"?variables={HttpUtility.UrlEncode(variablesJson)}" +
                     $"&extensions={HttpUtility.UrlEncode(extensionsJson)}";

        string response = await _httpClient.GetStringAsync(url);
        if (HasEpisodeSourcePayload(response))
            return response;

        return await PostEpisodeSourcesAsync(variables);
    }

    private async Task<string> PostEpisodeSourcesAsync(object variables)
    {
        string body = JsonSerializer.Serialize(new
        {
            variables,
            query = EPISODE_SOURCES_POST_GQL
        });

        using StringContent content = new(body, Encoding.UTF8, "application/json");
        using HttpResponseMessage httpResponse = await _httpClient.PostAsync($"{ALLANIME_API}/api", content);
        httpResponse.EnsureSuccessStatusCode();
        return await httpResponse.Content.ReadAsStringAsync();
    }

    private static bool HasEpisodeSourcePayload(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return false;

        try
        {
            using JsonDocument jsonDoc = JsonDocument.Parse(response);
            JsonElement root = jsonDoc.RootElement;

            if (TryGetTobeparsed(root, out _))
                return true;

            if (root.TryGetProperty("data", out JsonElement data))
            {
                if (TryGetTobeparsed(data, out _))
                    return true;

                if (data.TryGetProperty("episode", out JsonElement episode) &&
                    episode.TryGetProperty("sourceUrls", out JsonElement sourceUrls) &&
                    sourceUrls.ValueKind == JsonValueKind.Array &&
                    sourceUrls.GetArrayLength() > 0)
                    return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static bool TryGetTobeparsed(JsonElement element, out string tobeparsed)
    {
        if (element.TryGetProperty("tobeparsed", out JsonElement tobeParsedProp))
        {
            tobeparsed = tobeParsedProp.GetString() ?? string.Empty;
            return !string.IsNullOrEmpty(tobeparsed);
        }

        tobeparsed = string.Empty;
        return false;
    }

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
                Id   = show.GetProperty("_id").GetString()  ?? string.Empty,
                Name = show.GetProperty("name").GetString() ?? string.Empty
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

            if (jsonDoc.RootElement.TryGetProperty("data", out JsonElement data)         &&
                data.TryGetProperty("show",                out JsonElement show)          &&
                show.TryGetProperty("availableEpisodesDetail", out JsonElement detail)    &&
                detail.TryGetProperty("sub",               out JsonElement subEps))
            {
                foreach (JsonElement ep in subEps.EnumerateArray())
                {
                    string? epString = ep.GetString();
                    if (float.TryParse(epString, out float epNum))
                    {
                        episodes.Add(new AllMangaEpisode
                        {
                            Id            = $"{showId}-{epString}",
                            Number        = (int)epNum,
                            Url           = $"{ALLANIME_REFR}/anime/{showId}/episodes/sub/{epString}",
                            ShowId        = showId,
                            EpisodeString = epString,
                            TotalEpisodes = subEps.GetArrayLength()
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

            string response = await ApqGetEpisodeSourcesAsync(variables);
            using JsonDocument jsonDoc = JsonDocument.Parse(response);
            JsonElement root = jsonDoc.RootElement;

            if (TryGetTobeparsed(root, out string rootTobeparsed))
            {
                string decrypted = DecryptTobeparsedAes256Ctr(rootTobeparsed);
                return await ExtractVideoUrlFromSourcesJson(decrypted);
            }

            if (root.TryGetProperty("data", out JsonElement data))
            {
                if (TryGetTobeparsed(data, out string dataTobeparsed))
                {
                    string decrypted = DecryptTobeparsedAes256Ctr(dataTobeparsed);
                    return await ExtractVideoUrlFromSourcesJson(decrypted);
                }

                if (data.TryGetProperty("episode", out JsonElement episode) &&
                    episode.TryGetProperty("sourceUrls", out JsonElement sourceUrls))
                {
                    string? videoLink = await ResolveFirstVideoLinkAsync(sourceUrls);
                    if (!string.IsNullOrEmpty(videoLink))
                        return videoLink;
                }
            }

            throw new Exception("No valid video sources found");
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to get video URL: {ex.Message}", ex);
        }
    }

    private static string DecryptTobeparsedAes256Ctr(string base64Data)
    {
        byte[] raw = Convert.FromBase64String(base64Data);
        if (raw.Length < 13 + 16)
            throw new Exception("tobeparsed payload too short");

        ReadOnlySpan<byte> iv12       = raw.AsSpan(1, 12);
        int                ctLen      = raw.Length - 13 - 16;
        ReadOnlySpan<byte> ciphertext = raw.AsSpan(13, ctLen);

        byte[] key = SHA256.HashData(Encoding.UTF8.GetBytes(TOBEPARSED_KEY_MATERIAL));
        byte[] plain = AesCtrTransform(key, iv12, ciphertext);
        return Encoding.UTF8.GetString(plain);
    }

    private static byte[] AesCtrTransform(byte[] key, ReadOnlySpan<byte> iv12, ReadOnlySpan<byte> ciphertext)
    {
        byte[] counter = new byte[16];
        iv12.CopyTo(counter);
        counter[12] = 0;
        counter[13] = 0;
        counter[14] = 0;
        counter[15] = 2;

        byte[] output = new byte[ciphertext.Length];
        using Aes aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;

        using ICryptoTransform encryptor = aes.CreateEncryptor();
        byte[] keystream = new byte[16];

        for (int offset = 0; offset < ciphertext.Length; offset += 16)
        {
            encryptor.TransformBlock(counter, 0, 16, keystream, 0);
            int blockLen = Math.Min(16, ciphertext.Length - offset);
            for (int i = 0; i < blockLen; i++)
                output[offset + i] = (byte)(ciphertext[offset + i] ^ keystream[i]);
            IncrementCtrBigEndian(counter);
        }

        return output;
    }

    private static void IncrementCtrBigEndian(byte[] counter)
    {
        for (int i = counter.Length - 1; i >= 0; i--)
        {
            counter[i]++;
            if (counter[i] != 0)
                break;
        }
    }

    private async Task<string?> ResolveFirstVideoLinkAsync(JsonElement sourceUrls)
    {
        foreach (JsonElement source in sourceUrls.EnumerateArray())
        {
            if (!source.TryGetProperty("sourceUrl", out JsonElement sourceUrl))
                continue;

            string? encodedUrl = sourceUrl.GetString();
            if (string.IsNullOrEmpty(encodedUrl))
                continue;

            string decodedUrl = DecodeSourceUrl(encodedUrl);
            string videoLink  = await GetLinksFromSource(decodedUrl);
            if (!string.IsNullOrEmpty(videoLink))
                return videoLink;
        }

        return null;
    }

    private async Task<string> ExtractVideoUrlFromSourcesJson(string decryptedJson)
    {
        if (!decryptedJson.TrimStart().StartsWith('[') &&
            !decryptedJson.TrimStart().StartsWith('{'))
        {
            string direct = await GetLinksFromSource(decryptedJson.Trim());
            if (!string.IsNullOrEmpty(direct)) return direct;
            throw new Exception("Decrypted payload is not JSON and not a valid source URL");
        }

        using JsonDocument doc = JsonDocument.Parse(decryptedJson);
        JsonElement root = doc.RootElement;

        IEnumerable<JsonElement> sources = Enumerable.Empty<JsonElement>();
        if (root.ValueKind == JsonValueKind.Array)
            sources = root.EnumerateArray();
        else if (root.TryGetProperty("sourceUrls", out JsonElement su))
            sources = su.EnumerateArray();
        else if (root.TryGetProperty("episode", out JsonElement ep) &&
                 ep.TryGetProperty("sourceUrls", out JsonElement epSu))
            sources = epSu.EnumerateArray();

        JsonElement[] sourceArr = sources.ToArray();
        int[] order = Enumerable.Range(0, sourceArr.Length).ToArray();
        Array.Sort(order, (a, b) =>
        {
            static double Priority(JsonElement s) =>
                s.TryGetProperty("priority", out JsonElement p) && p.TryGetDouble(out double d) ? d : 0;
            static int TypeRank(JsonElement s)
            {
                if (!s.TryGetProperty("type", out JsonElement t))
                    return 0;
                return t.GetString() switch
                {
                    "player" => 2,
                    "iframe" => 0,
                    _        => 1
                };
            }
            static int SourceNameRank(JsonElement s)
            {
                if (!s.TryGetProperty("sourceName", out JsonElement nameProp))
                    return 0;

                return nameProp.GetString() switch
                {
                    "Default" => 6,
                    "Yt-mp4"  => 5,
                    "S-mp4"   => 4,
                    "Vid-mp4" => 3,
                    "Fm-Hls"  => 2,
                    "Mp4"     => 0,
                    _         => 1
                };
            }

            int c = SourceNameRank(sourceArr[b]).CompareTo(SourceNameRank(sourceArr[a]));
            if (c != 0) return c;
            c = Priority(sourceArr[b]).CompareTo(Priority(sourceArr[a]));
            return c != 0 ? c : TypeRank(sourceArr[b]).CompareTo(TypeRank(sourceArr[a]));
        });

        foreach (int idx in order)
        {
            JsonElement source = sourceArr[idx];
            string? rawUrl = null;

            if (source.TryGetProperty("sourceUrl", out JsonElement suProp))
                rawUrl = suProp.GetString();
            else if (source.ValueKind == JsonValueKind.String)
                rawUrl = source.GetString();

            if (string.IsNullOrEmpty(rawUrl)) continue;

            string decodedUrl = DecodeSourceUrl(rawUrl);
            string videoLink  = await GetLinksFromSource(decodedUrl);
            if (!string.IsNullOrEmpty(videoLink))
                return videoLink;
        }

        throw new Exception("No working video source found in decrypted payload");
    }

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
        if (string.IsNullOrWhiteSpace(sourceUrl))
            return string.Empty;

        if (Uri.TryCreate(sourceUrl, UriKind.Absolute, out Uri? absolute) &&
            (absolute.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
             absolute.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            string host = absolute.IdnHost;
            bool onAllanime = host.Equals(ALLANIME_BASE, StringComparison.OrdinalIgnoreCase)
                || host.EndsWith("." + ALLANIME_BASE, StringComparison.OrdinalIgnoreCase);

            if (!onAllanime)
                return await ResolvePlayableUrlAsync(NormalizeHttpUrl(sourceUrl));

            sourceUrl = string.Concat(absolute.PathAndQuery, absolute.Fragment);
        }

        if (!sourceUrl.StartsWith('/'))
            sourceUrl = "/" + sourceUrl;

        try
        {
            string response = await _httpClient.GetStringAsync($"https://{ALLANIME_BASE}{sourceUrl}");
            using JsonDocument jsonDoc = JsonDocument.Parse(response);

            if (jsonDoc.RootElement.TryGetProperty("links", out JsonElement links))
            {
                string? bestUrl = null;
                int bestResolution = -1;

                foreach (JsonElement link in links.EnumerateArray())
                {
                    if (!link.TryGetProperty("link", out JsonElement linkProp))
                        continue;

                    string? url = linkProp.GetString();
                    if (string.IsNullOrEmpty(url))
                        continue;

                    int resolution = 0;
                    if (link.TryGetProperty("resolutionStr", out JsonElement resolutionProp))
                    {
                        string? resolutionText = resolutionProp.GetString();
                        if (!string.IsNullOrEmpty(resolutionText))
                            _ = int.TryParse(new string(resolutionText.TakeWhile(char.IsDigit).ToArray()), out resolution);
                    }

                    if (resolution >= bestResolution)
                    {
                        bestResolution = resolution;
                        bestUrl = NormalizeHttpUrl(url);
                    }
                }

                if (!string.IsNullOrEmpty(bestUrl))
                    return await ResolvePlayableUrlAsync(bestUrl, response);
            }

            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task<string> ResolvePlayableUrlAsync(string url, string? clockJson = null)
    {
        url = NormalizeHttpUrl(url);
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        if (url.Contains("mp4upload.com", StringComparison.OrdinalIgnoreCase) &&
            url.Contains("/embed", StringComparison.OrdinalIgnoreCase))
        {
            string resolved = await ResolveMp4UploadEmbedAsync(url);
            return IsDirectMediaUrl(resolved) ? resolved : string.Empty;
        }

        if (url.Contains("repackager.wixmp.com", StringComparison.OrdinalIgnoreCase))
        {
            string resolved = ResolveWixmpUrl(url);
            if (IsDirectMediaUrl(resolved))
                return resolved;
        }

        if (url.Contains("master.m3u8", StringComparison.OrdinalIgnoreCase))
        {
            string referer = ALLANIME_REFR + "/";
            if (!string.IsNullOrEmpty(clockJson))
            {
                Match refererMatch = ClockRefererRegex.Match(clockJson);
                if (refererMatch.Success)
                    referer = refererMatch.Groups[1].Value;
            }

            string resolved = await ResolveMasterPlaylistAsync(url, referer);
            if (IsDirectMediaUrl(resolved))
                return resolved;
        }

        return IsDirectMediaUrl(url) ? url : string.Empty;
    }

    private async Task<string> ResolveMp4UploadEmbedAsync(string embedUrl)
    {
        try
        {
            string html = await _httpClient.GetStringAsync(embedUrl);
            Match match = Mp4UploadSrcRegex.Match(html);
            if (!match.Success)
                return string.Empty;

            return NormalizeHttpUrl(match.Groups[1].Value.Replace("\\/", "/", StringComparison.Ordinal));
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ResolveWixmpUrl(string url)
    {
        if (url.Contains(".mp4", StringComparison.OrdinalIgnoreCase) &&
            !url.Contains("urlset", StringComparison.OrdinalIgnoreCase))
            return url;

        if (url.Contains("master.m3u8", StringComparison.OrdinalIgnoreCase))
            return url;

        Match match = WixmpUrlsetRegex.Match(url);
        if (!match.Success)
            return string.Empty;

        string quality = match.Groups[1].Value;
        int urlsetIndex = url.IndexOf("/urlset/", StringComparison.OrdinalIgnoreCase);
        if (urlsetIndex < 0)
            return string.Empty;

        string prefix = url[..urlsetIndex];
        return $"{prefix}/{quality}/{quality}.mp4";
    }

    private async Task<string> ResolveMasterPlaylistAsync(string masterUrl, string referer)
    {
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, masterUrl);
            request.Headers.TryAddWithoutValidation("Referer", referer);
            request.Headers.TryAddWithoutValidation("Origin", ALLANIME_REFR);

            using HttpResponseMessage response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            string playlist = await response.Content.ReadAsStringAsync();

            if (!playlist.Contains("#EXTM3U", StringComparison.Ordinal))
                return string.Empty;

            Uri baseUri = new(masterUrl);
            string? bestUrl = null;
            int bestBandwidth = -1;
            string? lastInf = null;

            foreach (string rawLine in playlist.Split('\n'))
            {
                string line = rawLine.Trim();
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
                    bestUrl = NormalizeHttpUrl(streamUrl);
                }

                lastInf = null;
            }

            return bestUrl ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsDirectMediaUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (url.Contains("/embed", StringComparison.OrdinalIgnoreCase))
            return false;

        if (url.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            return false;

        return url.Contains(".m3u8", StringComparison.OrdinalIgnoreCase)
            || url.Contains(".mp4", StringComparison.OrdinalIgnoreCase)
            || url.Contains("tools.fast4speed", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeHttpUrl(string url)
    {
        int scheme = url.IndexOf("://", StringComparison.Ordinal);
        if (scheme < 0)
            return url;

        int pathStart = url.IndexOf('/', scheme + 3);
        if (pathStart < 0)
            return url;

        int i = pathStart;
        while (i + 1 < url.Length && url[i] == '/' && url[i + 1] == '/')
            i++;

        if (i == pathStart)
            return url;

        return string.Concat(url.AsSpan(0, pathStart + 1), url.AsSpan(i + 1));
    }
}