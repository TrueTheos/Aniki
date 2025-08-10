using Aniki.Misc;
using Avalonia.Media.Imaging;

namespace Aniki.Services.Interfaces;

public interface IMalService
{
    public void Init(string accessToken);
    public Task<MALUserData> GetUserDataAsync();
    public Task<List<AnimeData>> GetUserAnimeList(AnimeStatusApi status = AnimeStatusApi.none);
    public Task<AnimeDetails?> GetAnimeDetails(int id, bool forceFull = false);
    public Task<string> GetAnimeNameById(int id);
    public Task<Bitmap?> GetUserPicture();
    public Task<Bitmap?> GetAnimeImage(MainPicture? animePictureData);
    public Task<List<SearchEntry>> SearchAnimeOrdered(string query);
    public Task UpdateAnimeStatus(int animeId, MalService.AnimeStatusField field, string value);
    public Task RemoveFromList(int animeId);
}