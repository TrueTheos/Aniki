using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Aniki.Services.Anime;
using Aniki.Services.Interfaces;

namespace Aniki.Services;

public class AllMangaScraperService : IAllMangaScraperService
{
    private readonly HttpClient _httpClient;
    private const string ALLANIME_BASE = "allanime.day";
    private const string ALLANIME_API = "https://api.allanime.day";
    private const string ALLANIME_REFR = "https://allmanga.to";

    public AllMangaScraperService()
    {
        HttpClientHandler handler = new()
        {
            AllowAutoRedirect = true,
            UseCookies = true
        };
        
        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", 
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/121.0");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.DefaultRequestHeaders.Add("Referer", ALLANIME_REFR);
    }
    
    public async Task<List<AllMangaSearchResult>> SearchAnimeAsync(string query)
    {
        try
        {
            const string searchGql = @"query($search: SearchInput $limit: Int $page: Int $translationType: VaildTranslationTypeEnumType $countryOrigin: VaildCountryOriginEnumType) { 
                shows(search: $search limit: $limit page: $page translationType: $translationType countryOrigin: $countryOrigin) { 
                    edges { 
                        _id 
                        name 
                        availableEpisodes
                        malId
                        banner
                        __typename 
                    } 
                }
            }";

            var variables = new
            {
                search = new
                {
                    allowAdult = false,
                    allowUnknown = false,
                    query = query
                },
                limit = 40,
                page = 1,
                translationType = "sub",
                countryOrigin = "ALL"
            };

            string requestUrl = $"{ALLANIME_API}/api?variables={HttpUtility.UrlEncode(JsonSerializer.Serialize(variables))}&query={HttpUtility.UrlEncode(searchGql)}";
            
            string response = await _httpClient.GetStringAsync(requestUrl);
            JsonDocument jsonDoc = JsonDocument.Parse(response);
            
            List<AllMangaSearchResult> results = new();
            
            if (jsonDoc.RootElement.TryGetProperty("data", out JsonElement data) &&
                data.TryGetProperty("shows", out JsonElement shows) &&
                shows.TryGetProperty("edges", out JsonElement edges))
            {
                foreach (JsonElement edge in edges.EnumerateArray())
                {
                    string? id = edge.GetProperty("_id").GetString();
                    string? name = edge.GetProperty("name").GetString();
                    
                    int? malId = null;
                    if (edge.TryGetProperty("malId", out JsonElement malIdProp) && 
                        malIdProp.ValueKind == JsonValueKind.String)
                    {
                        if (malIdProp.ValueKind == JsonValueKind.String)
                        {
                            string? malIdString = malIdProp.GetString();
                            if (malIdString != null)
                            {
                                malId = Int32.Parse(malIdString);
                            }
                        }
                        else if (malIdProp.ValueKind == JsonValueKind.Number)
                        {
                            malId = malIdProp.GetInt32();
                        }
                        else
                        {
                            Log.Error($"{name} has wrong malidtype {malIdProp.ValueKind}");
                        }
                    }
                    
                    int episodeCount = 0;
                    if (edge.TryGetProperty("availableEpisodes", out JsonElement availableEpisodes) &&
                        availableEpisodes.TryGetProperty("sub", out JsonElement subCount))
                    {
                        episodeCount = subCount.GetInt32();
                    }

                    string? banner = null;
                    if (edge.TryGetProperty("banner", out JsonElement bannerString))
                    {
                        banner = bannerString.GetString();
                    }
                    
                    if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name))
                    {
                        results.Add(new AllMangaSearchResult
                        {
                            Id = id,
                            Title = $"{name}",
                            Episodes = episodeCount,
                            Url = $"{ALLANIME_REFR}/anime/{id}",
                            MalId = malId,
                            Banner = banner
                        });
                    }
                }
            }

            results = results
                .Select(x => new { Item = x, Score = CalculateAnimeScore(x, query) })
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Item.Episodes)
                .Select(x => x.Item)
                .ToList();
            
            return results;
        }
        catch (Exception ex)
        {
            throw new Exception($"Search failed: {ex.Message}", ex);
        }
    }

    public async Task<AllMangaAnimeDetails?> GetAnimeDetailsAsync(string animeIdOrUrl)
    {
        try
        {
            string showId = animeIdOrUrl.Contains("/") 
                ? animeIdOrUrl.Split('/').Last() 
                : animeIdOrUrl;

            const string detailsGql = @"query($showId: String!) { 
                show(_id: $showId) { 
                    _id
                    name
                    malId
                    aniListId
                    description
                    availableEpisodesDetail
                    thumbnail
                    __typename
                }
            }";

            var variables = new
            {
                showId = showId
            };

            string requestUrl = $"{ALLANIME_API}/api?variables={HttpUtility.UrlEncode(JsonSerializer.Serialize(variables))}&query={HttpUtility.UrlEncode(detailsGql)}";
            
            string response = await _httpClient.GetStringAsync(requestUrl);
            JsonDocument jsonDoc = JsonDocument.Parse(response);
            
            if (jsonDoc.RootElement.TryGetProperty("data", out JsonElement data) &&
                data.TryGetProperty("show", out JsonElement show))
            {
                AllMangaAnimeDetails details = new()
                {
                    Id = show.GetProperty("_id").GetString() ?? string.Empty,
                    Name = show.GetProperty("name").GetString() ?? string.Empty
                };

                if (show.TryGetProperty("malId", out JsonElement malIdProp) && 
                    malIdProp.ValueKind == JsonValueKind.Number)
                {
                    details.MalId = malIdProp.GetInt32();
                }

                if (show.TryGetProperty("aniListId", out JsonElement aniListIdProp) && 
                    aniListIdProp.ValueKind == JsonValueKind.Number)
                {
                    details.AniListId = aniListIdProp.GetInt32();
                }

                if (show.TryGetProperty("description", out JsonElement descProp))
                {
                    details.Description = descProp.GetString();
                }
                
                if (show.TryGetProperty("thumbnail", out JsonElement thumbProp))
                {
                    details.Thumbnail = thumbProp.GetString();
                }

                return details;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to get anime details: {ex.Message}", ex);
        }
    }

    public async Task<List<AllManagaEpisode>> GetEpisodesAsync(string animeIdOrUrl)
    {
        try
        {
            string showId = animeIdOrUrl.Contains("/") 
                ? animeIdOrUrl.Split('/').Last() 
                : animeIdOrUrl;

            const string episodesGql = @"query($showId: String!) { 
                show(_id: $showId) { 
                    _id 
                    availableEpisodesDetail 
                }
            }";

            var variables = new
            {
                showId = showId
            };

            string requestUrl = $"{ALLANIME_API}/api?variables={HttpUtility.UrlEncode(JsonSerializer.Serialize(variables))}&query={HttpUtility.UrlEncode(episodesGql)}";
            
            string response = await _httpClient.GetStringAsync(requestUrl);
            JsonDocument jsonDoc = JsonDocument.Parse(response);
            
            List<AllManagaEpisode> episodes = new();
            
            if (jsonDoc.RootElement.TryGetProperty("data", out JsonElement data) &&
                data.TryGetProperty("show", out JsonElement show) &&
                show.TryGetProperty("availableEpisodesDetail", out JsonElement episodesDetail) &&
                episodesDetail.TryGetProperty("sub", out JsonElement subEpisodes))
            {
                foreach (JsonElement ep in subEpisodes.EnumerateArray())
                {
                    string? epString = ep.GetString();
                    if (float.TryParse(epString, out float epNum))
                    {
                        episodes.Add(new AllManagaEpisode
                        {
                            Id = $"{showId}-{epString}",
                            Number = (int)epNum,
                            Url = $"{ALLANIME_REFR}/anime/{showId}/episodes/sub/{epString}",
                            ShowId = showId,
                            EpisodeString = epString,
                            TotalEpisodes = subEpisodes.GetArrayLength()
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

    private int CalculateAnimeScore(AllMangaSearchResult anime, string query)
    {
        if (DoesTitleMatch(anime, query))
        {
            return 1000;
        }
        
        int score = FuzzySharp.Fuzz.TokenSortRatio(anime.Title, query);
        
        if (anime.Title != null && anime.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            score += 50;
        }

        return score;
    }

    private bool DoesTitleMatch(AllMangaSearchResult malAnime, string query)
    {
        string normalizedQuery = NormalizeTitleToLower(query);
        string normalizedTitle = NormalizeTitleToLower(malAnime.Title);

        return normalizedTitle == normalizedQuery;
    }

    private string NormalizeTitleToLower(string? title)
    {
        if (string.IsNullOrEmpty(title)) return string.Empty;
    
        string normalized = title.Replace("-", "").Replace("_", "").Replace(":", "").Trim();
        normalized = Regex.Replace(normalized, @"\s+", " ");
        return normalized.ToLower();
    }
    
    public async Task<string> GetVideoUrlAsync(string episodeUrl)
    {
        try
        {
            // Expected format: https://allmanga.to/anime/{showId}/episodes/sub/{episodeString}
            string[] parts = episodeUrl.Split('/');
            string showId = parts[^4];
            string episodeString = parts[^1];

            const string episodeGql = @"query($showId: String!, $translationType: VaildTranslationTypeEnumType!, $episodeString: String!) { 
                episode(showId: $showId translationType: $translationType episodeString: $episodeString) { 
                    episodeString 
                    sourceUrls 
                }
            }";

            var variables = new
            {
                showId = showId,
                translationType = "sub",
                episodeString = episodeString
            };

            string requestUrl = $"{ALLANIME_API}/api?variables={HttpUtility.UrlEncode(JsonSerializer.Serialize(variables))}&query={HttpUtility.UrlEncode(episodeGql)}";
            
            string response = await _httpClient.GetStringAsync(requestUrl);
            
            JsonDocument jsonDoc = JsonDocument.Parse(response);
            
            if (jsonDoc.RootElement.TryGetProperty("data", out JsonElement data) &&
                data.TryGetProperty("episode", out JsonElement episode) &&
                episode.TryGetProperty("sourceUrls", out JsonElement sourceUrls))
            {
                foreach (JsonElement source in sourceUrls.EnumerateArray())
                {
                    if (source.TryGetProperty("sourceUrl", out JsonElement sourceUrl))
                    {
                        string? encodedUrl = sourceUrl.GetString();
                        if (!string.IsNullOrEmpty(encodedUrl))
                        {
                            string decodedUrl = DecodeSourceUrl(encodedUrl);
                            
                            string videoLink = await GetLinksFromSource(decodedUrl);
                            if (!string.IsNullOrEmpty(videoLink))
                                return videoLink;
                        }
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

    private string DecodeSourceUrl(string encoded)
    {
        if (string.IsNullOrEmpty(encoded) || !encoded.StartsWith("--"))
            return encoded;

        encoded = encoded.Substring(2);
        StringBuilder decoded = new();
        
        for (int i = 0; i < encoded.Length; i += 2)
        {
            if (i + 1 < encoded.Length)
            {
                string hex = encoded.Substring(i, 2);
                string chr = DecodeHexChar(hex);
                if (!string.IsNullOrEmpty(chr))
                    decoded.Append(chr);
            }
        }
        
        return decoded.ToString().Replace("/clock", "/clock.json");
    }

    private string DecodeHexChar(string hex)
    {
        return hex switch
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
    }

    private async Task<string> GetLinksFromSource(string sourceUrl)
    {
        try
        {
            string response = await _httpClient.GetStringAsync($"https://{ALLANIME_BASE}{sourceUrl}");
            JsonDocument jsonDoc = JsonDocument.Parse(response);
            
            if (jsonDoc.RootElement.TryGetProperty("links", out JsonElement links))
            {
                foreach (JsonElement link in links.EnumerateArray())
                {
                    if (link.TryGetProperty("link", out JsonElement linkProp) &&
                        link.TryGetProperty("resolutionStr", out JsonElement resolution))
                    {
                        string? url = linkProp.GetString();
                        if (!string.IsNullOrEmpty(url))
                        {
                            return url;
                        }
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