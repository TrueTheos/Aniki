using System.Collections.Concurrent;
using Avalonia.Media.Imaging;
using System.Text.Json.Serialization;
using Aniki.Services.Interfaces;

namespace Aniki.Services;

    
public struct SeasonData
{
    public int MalId { get; set; }
    public int Episodes { get; set; }
}

public class AnimeSeasonsMap
{
    public enum AnimeSeasonMapField
    {
        SeasonData
    }

    [CacheField(AnimeSeasonMapField.SeasonData)]
    public Dictionary<int, SeasonData> Seasons { get; set; } = new();
}

public class SaveService : ISaveService
{
    public static readonly string MAIN_DIRECTORY = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Aniki");

    public string DefaultEpisodesFolder => Path.Combine(MAIN_DIRECTORY, "Episodes");
    private string _imageCacheFolder => Path.Combine(MAIN_DIRECTORY, "ImageCache");

    private ImageSaver _imageSaver;
    private SaveEntity<SettingsConfig> _settingsSaver;
    public GenericCacheService<string, AnimeSeasonsMap, AnimeSeasonsMap.AnimeSeasonMapField> SeasonCache { get; private set; }

    private readonly ConcurrentDictionary<Type, ICacheService> _caches;

    public SaveService()
    {
        _caches = new ConcurrentDictionary<Type, ICacheService>();
        SeasonCache = CreateCache<string, AnimeSeasonsMap, AnimeSeasonsMap.AnimeSeasonMapField>(null);

        _imageSaver = (ImageSaver)CreateSaveEntity<Avalonia.Media.Imaging.Bitmap>(_imageCacheFolder);
        _settingsSaver = CreateSaveEntity<SettingsConfig>(MAIN_DIRECTORY);

        EnsureDirectoriesExist();
    }

    private SaveEntity<T> CreateSaveEntity<T>(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        return typeof(T).Name switch
        {
            nameof(Avalonia.Media.Imaging.Bitmap) => (SaveEntity<T>)(object)new ImageSaver(path),
            _ => new SaveEntity<T>(path)
        };
    }
    
    public GenericCacheService<TKey, TEntity, TFieldEnum> CreateCache<TKey, TEntity, TFieldEnum>
        (Func<TKey, TFieldEnum[], Task<TEntity?>>? fetchHandler, CacheOptions? options = null)
        where TKey : notnull
        where TEntity : class, new()
        where TFieldEnum : Enum
    {
        Type cacheType = typeof(GenericCacheService<TKey, TEntity, TFieldEnum>);

        ICacheService cache = _caches.GetOrAdd(cacheType, _ =>
            new GenericCacheService<TKey, TEntity, TFieldEnum>(fetchHandler, options));

        return (GenericCacheService<TKey, TEntity, TFieldEnum>)cache;
    }

    private void EnsureDirectoriesExist()
    {
        if (!Directory.Exists(MAIN_DIRECTORY))
            Directory.CreateDirectory(MAIN_DIRECTORY);

        if (!Directory.Exists(DefaultEpisodesFolder))
            Directory.CreateDirectory(DefaultEpisodesFolder);
    }

    public GenericCacheService<string, AnimeSeasonsMap, AnimeSeasonsMap.AnimeSeasonMapField> GetSeasonCache() => SeasonCache;

    public void SaveSettings(SettingsConfig config) => _settingsSaver.Save("config.json", config);
    public SettingsConfig? GetSettingsConfig() => _settingsSaver.Read("config.json");

    public bool TryGetAnimeImage(int id, out Bitmap? picture)
    {
        string fileName = $"anime_{id}.jpg";

        picture = _imageSaver.Read(fileName);

        return picture != null;
    }

    public void SaveImage(int id, Bitmap image)
    {
        string fileName = $"anime_{id}.jpg";
        _imageSaver.Save(fileName, image);
    }
}