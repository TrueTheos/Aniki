using Aniki.Services.Cache;
using Aniki.Services.Parser;
using Aniki.Services.Save;
using Avalonia.Media.Imaging;

namespace Aniki.Services.Interfaces;

internal interface ISaveService
{
    public string DefaultEpisodesFolder { get; }
    public GenericCacheService<string, AnimeSeasonsMap, AnimeSeasonsMap.AnimeSeasonMapField> GetSeasonCache();
    public void SaveSettings(SettingsConfig config);
    public SettingsConfig? GetSettingsConfig();
    public bool TryGetAnimeImage(int id, out Bitmap? picture);
    public void SaveImage(int id, Bitmap image);
    public void RegisterCache(ICacheService cache);
    public Task ClearAllCaches();
    public Task SaveAllCaches();
    public void DeleteFolders();
}