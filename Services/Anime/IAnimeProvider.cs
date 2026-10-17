using Aniki.Services.Auth;

namespace Aniki.Services.Anime;

internal interface IAnimeProvider
{
    ILoginProvider.ProviderType Provider { get; }
    bool IsLoggedIn { get; }
    
    Task InitAsync(string? accessToken);
    
    Task<UserData> GetUserDataAsync();
    Task<List<AnimeDetails>> GetUserAnimeListAsync(AnimeStatus status = AnimeStatus.None);
    Task RemoveFromUserListAsync(int animeId);
    Task<AnimeDetails?> FetchAnimeDetailsAsync(int animeId, params AnimeField[] fields);
    Task<List<AnimeDetails>> SearchAnimeAsync(string query);
    Task<List<RankingEntry>> GetTopAnimeAsync(RankingCategory category);
    Task<int?> GetIdFromMalIdAsync(int malId);
    Task SetAnimeStatusAsync(int animeId, AnimeStatus status);
    Task SetAnimeScoreAsync(int animeId, int score);
    Task SetEpisodesWatchedAsync(int animeId, int episodes);
    void ClearRuntimeCache();
}