using Aniki.Misc;
using Aniki.Models.MAL;
using Avalonia.Media.Imaging;

namespace Aniki.Services.Interfaces;

public interface IMalService
{
    public void Init(string? accessToken);
    public Task<MAL_UserData> GetUserDataAsync();
    public Task<List<MAL_AnimeData>> GetUserAnimeList(AnimeStatusApi status = AnimeStatusApi.none);
    public Task<AnimeFieldSet> GetAllFieldsAsync(int animeId);
    public Task<AnimeFieldSet> GetFieldsAsync(int animeId, params AnimeField[] fields);
    public Task<List<MAL_SearchEntry>> SearchAnimeOrdered(string query);
    public Task SetAnimeStatus(int animeId, AnimeStatusApi status);
    public Task SetAnimeScore(int animeId, int score);
    public Task SetEpisodesWatched(int animeId, int episodes);
    public Task RemoveFromUserList(int animeId);
    public Task<List<MAL_RankingEntry>> GetTopAnimeInCategory(MalService.AnimeRankingCategory category, int limit = 10);
    public void SubscribeToFieldChange(int animeId, AnimeField field, EventHandler<AnimeFieldSet> handler);
    public void UnsubscribeFromFieldChange(int animeId, AnimeField field, EventHandler<AnimeFieldSet> handler);
}