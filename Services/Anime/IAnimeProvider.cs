using Aniki.Services.Auth;
using Avalonia.Media.Imaging;

namespace Aniki.Services.Anime;

public interface IAnimeProvider
{
    ILoginProvider.ProviderType Provider { get; }
    bool IsLoggedIn { get; }
    
    void Init(string accessToken);
    
    Task<UserData> GetUserDataAsync();
    Task<List<AnimeData>> GetUserAnimeListAsync(AnimeStatus status = AnimeStatus.None);
    Task RemoveFromUserListAsync(int animeId);
    Task<AnimeDetails?> FetchAnimeDetailsAsync(int animeId, params AnimeField[] fields);
    Task<List<AnimeSearchResult>> SearchAnimeAsync(string query);
    Task<List<RankingEntry>> GetTopAnimeAsync(RankingCategory category, int limit = 10);
    Task<Bitmap?> LoadAnimeImageAsync(int animeId, string? imageUrl);
    Task SetAnimeStatusAsync(int animeId, AnimeStatus status);
    Task SetAnimeScoreAsync(int animeId, int score);
    Task SetEpisodesWatchedAsync(int animeId, int episodes);
}