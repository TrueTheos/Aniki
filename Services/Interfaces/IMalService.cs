namespace Aniki.Services.Interfaces;

public interface IMalService
{
    public void Init(string? accessToken);
    public Task<MAL_UserData> GetUserDataAsync();
    public Task<List<MalAnimeData>> GetUserAnimeList(AnimeStatusApi status = AnimeStatusApi.none);
    public Task<MalAnimeDetails> GetAllFieldsAsync(int animeId);
    public Task<MalAnimeDetails> GetFieldsAsync(int animeId, params AnimeField[] fields);
    public Task<List<MalSearchEntry>> SearchAnimeOrdered(string query);
    public Task SetAnimeStatus(int animeId, AnimeStatusApi status);
    public Task SetAnimeScore(int animeId, int score);
    public Task SetEpisodesWatched(int animeId, int episodes);
    public Task RemoveFromUserList(int animeId);
    public Task<List<MAL_RankingEntry>> GetTopAnimeInCategory(MalService.AnimeRankingCategory category, int limit = 10);
    public void SubscribeToFieldChange(int animeId, FieldChangeHandler<MalAnimeDetails> handler, params AnimeField[] fields);
    public void UnsubscribeFromFieldChange(int animeId, FieldChangeHandler<MalAnimeDetails> handler, params AnimeField[] fields);
}