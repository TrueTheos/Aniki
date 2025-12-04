using Aniki.Services.Auth;
using Aniki.Services.Cache;

namespace Aniki.Services.Anime;

public interface IAnimeService
{
    public Task<List<AnimeDetails>> GetUserAnimeListAsync(AnimeStatus status = AnimeStatus.None);
    public Task<AnimeDetails?> GetFieldsAsync(int animeId, bool forceFetch = false, params AnimeField[] fields);
    public void RegisterProvider(ILoginProvider.ProviderType name, IAnimeProvider provider);
    public Task<List<AnimeDetails>> SearchAnimeAsync(string query);

    public void SubscribeToFieldChange(int animeId, FieldChangeHandler<AnimeDetails> handler,
        params AnimeField[] fields);

    public void UnsubscribeFromFieldChange(int animeId, FieldChangeHandler<AnimeDetails> handler,
        params AnimeField[] fields);

    public void SetActiveProvider(ILoginProvider.ProviderType providerName, string? accessToken);
    public Task<UserData> GetUserDataAsync();
    public Task<List<RankingEntry>> GetTopAnimeAsync(RankingCategory category, int limit);
    public Task<AnimeDetails> GetAllFieldsAsync(int animeId);
    public Task RemoveFromUserListAsync(int animeId);
    public Task SetAnimeStatusAsync(int animeId, AnimeStatus status);
    public Task SetAnimeScoreAsync(int animeId, int score);
    public Task SetEpisodesWatchedAsync(int animeId, int episodes);
}