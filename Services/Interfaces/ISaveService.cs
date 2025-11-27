using Avalonia.Media.Imaging;

namespace Aniki.Services.Interfaces;

public interface ISaveService
{
    public CacheManager? ImageCache { get; }
    public string DefaultEpisodesFolder { get; }
    
    public SeasonCache GetSeasonCache();
    public void SaveSeasonCache(SeasonCache cache);
    public void SaveSettings(SettingsConfig config);
    public SettingsConfig? GetSettingsConfig();
    public bool TryGetAnimeImage(int id, out Bitmap? picture);
    public void SaveImage(int id, Bitmap image);
}