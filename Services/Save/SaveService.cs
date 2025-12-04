using System.Collections.Concurrent;
using Aniki.Services.Anime;
using Aniki.Services.Auth;
using Aniki.Services.Cache;
using Aniki.Services.Interfaces;
using Avalonia.Media.Imaging;

namespace Aniki.Services.Save;

    
public struct SeasonData
{
    public int Id { get; set; }
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
    public static readonly string MainDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Aniki");
    
    public static readonly string CachePath = Path.Combine(MainDirectory, "cache");
    public static readonly string TokenDirectoryPath = Path.Combine(MainDirectory, "tokens");

    public string DefaultEpisodesFolder => Path.Combine(MainDirectory, "Episodes");
    private string ImageCacheFolder => Path.Combine(MainDirectory, "ImageCache");
    
    private readonly ImageSaver _imageSaver;
    private readonly SaveEntity<SettingsConfig> _settingsSaver;

    private Dictionary<ILoginProvider.ProviderType,
        GenericCacheService<string, AnimeSeasonsMap, AnimeSeasonsMap.AnimeSeasonMapField>> _seasonsCache;

    private readonly ConcurrentDictionary<Guid, ICacheService> _caches;

    public SaveService()
    {
        _caches = new ConcurrentDictionary<Guid, ICacheService>();
        _seasonsCache = new();
        
        foreach (ILoginProvider.ProviderType providerType in Enum.GetValues<ILoginProvider.ProviderType>())
        {
            CacheOptions options = new()
            {
                DiskCachePath = $"{CachePath}/SeasonMaps/{providerType}",
            };
            _seasonsCache[providerType] = CreateCache<string, AnimeSeasonsMap, AnimeSeasonsMap.AnimeSeasonMapField>(fetchHandler:null, options: options);
        }

        _imageSaver = (ImageSaver)CreateSaveEntity<Bitmap>(ImageCacheFolder);
        _settingsSaver = CreateSaveEntity<SettingsConfig>(MainDirectory);

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
            nameof(Bitmap) => (SaveEntity<T>)(object)new ImageSaver(path),
            _ => new SaveEntity<T>(path)
        };
    }
    
    public GenericCacheService<TKey, TEntity, TFieldEnum> CreateCache<TKey, TEntity, TFieldEnum>
        (Func<TKey, TFieldEnum[], Task<TEntity?>>? fetchHandler, CacheOptions? options = null)
        where TKey : notnull
        where TEntity : class, new()
        where TFieldEnum : Enum
    {
        ICacheService cache = _caches.GetOrAdd(Guid.NewGuid(), _ =>
            new GenericCacheService<TKey, TEntity, TFieldEnum>(fetchHandler, options));

        return (GenericCacheService<TKey, TEntity, TFieldEnum>)cache;
    }

    private void EnsureDirectoriesExist()
    {
        if (!Directory.Exists(MainDirectory))
            Directory.CreateDirectory(MainDirectory);

        if (!Directory.Exists(DefaultEpisodesFolder))
            Directory.CreateDirectory(DefaultEpisodesFolder);
    }

    public GenericCacheService<string, AnimeSeasonsMap, AnimeSeasonsMap.AnimeSeasonMapField> GetSeasonCache() => _seasonsCache[AnimeService.CurrentProviderType];

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

    public async Task FlushAllCaches()
    {
        foreach (KeyValuePair<Guid, ICacheService> cache in _caches)
        {
            await cache.Value.FlushAsync();
        }
    }

    public void Wipe()
    {
        DeleteFolder(CachePath);
        DeleteFolder(ImageCacheFolder);
        DeleteFolder(TokenDirectoryPath);
    }

    private void DeleteFolder(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"Access denied: {ex.Message}");
        }
    }
}