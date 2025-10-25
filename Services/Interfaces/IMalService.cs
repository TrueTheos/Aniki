using Aniki.Misc;
using Avalonia.Media.Imaging;

namespace Aniki.Services.Interfaces;

public interface IMalService
{
    public void Init(string accessToken);
    public Task<MAL_UserData> GetUserDataAsync();
    public Task<List<MAL_AnimeData>> GetUserAnimeList(AnimeStatusApi status = AnimeStatusApi.none);
    public Task<MAL_AnimeDetails?> GetAnimeDetails(int id, bool forceFull = false);
    public Task<string> GetAnimeNameById(int id);
    public Task<Bitmap?> GetUserPicture();
    public Task<Bitmap?> GetAnimeImage(MAL_MainPicture? animePictureData);
    public Task<List<MAL_SearchEntry>> SearchAnimeOrdered(string query);
    public Task UpdateAnimeStatus(int animeId, AnimeStatusApi status);
    public Task UpdateAnimeScore(int animeId, int score);
    public Task UpdateEpisodesWatched(int animeId, int episodes);
    public Task RemoveFromList(int animeId);
    public Task<List<MAL_RankingEntry>> GetTopAnimeInCategory(MalService.AnimeRankingCategory category, int limit = 10);
}