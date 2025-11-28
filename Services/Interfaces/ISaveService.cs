using Avalonia.Media.Imaging;

namespace Aniki.Services.Interfaces;

public interface ISaveService
{
    public string DefaultEpisodesFolder { get; }
    public GenericCacheService<string, AnimeSeasonsMap, AnimeSeasonsMap.AnimeSeasonMapField> GetSeasonCache();
    public void SaveSettings(SettingsConfig config);
    public SettingsConfig? GetSettingsConfig();
    public bool TryGetAnimeImage(int id, out Bitmap? picture);
    public void SaveImage(int id, Bitmap image);
    public Task FlushAllCaches();
}