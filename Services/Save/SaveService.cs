using System.Collections.Concurrent;
using Aniki.Services.Anime;
using Aniki.Services.Auth;
using Aniki.Services.Cache;
using Aniki.Services.Interfaces;
using Aniki.Services.Parser;
using Avalonia.Media.Imaging;

namespace Aniki.Services.Save;

internal sealed class SaveService : ISaveService
{
    private static readonly string MainDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Aniki");
    
    public static readonly string CachePath = Path.Combine(MainDirectory, "cache");
    public static readonly string TokenDirectoryPath = Path.Combine(MainDirectory, "tokens");

    public string DefaultEpisodesFolder => Path.Combine(MainDirectory, "Episodes");
    private static string ImageCacheFolder => Path.Combine(MainDirectory, "ImageCache");
    
    private readonly ImageSaver _imageSaver;
    private readonly SaveEntity<SettingsConfig> _settingsSaver;

    private readonly Dictionary<ILoginProvider.ProviderType,
        GenericCacheService<string, AnimeSeasonsMap, AnimeSeasonsMap.AnimeSeasonMapField>> _seasonsCache;

    private readonly ConcurrentDictionary<Guid, ICacheService> _caches;

    public SaveService()
    {
        _caches = new ConcurrentDictionary<Guid, ICacheService>();
        _seasonsCache = new Dictionary<ILoginProvider.ProviderType, GenericCacheService<string, AnimeSeasonsMap, AnimeSeasonsMap.AnimeSeasonMapField>>();
        
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

    private GenericCacheService<TKey, TEntity, TFieldEnum> CreateCache<TKey, TEntity, TFieldEnum>
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

    public void RegisterCache(ICacheService cache)
    {
        _caches.TryAdd(Guid.NewGuid(), cache);
    }

    public async Task ClearAllCaches()
    {
        foreach (var cache in _caches)
        {
            await cache.Value.ClearAllAsync().ConfigureAwait(false);
        }

        DeleteFolder(ImageCacheFolder);
        Directory.CreateDirectory(ImageCacheFolder);
    }

    public async Task SaveAllCaches()
    {
        foreach (var cache in _caches)
        {
            await cache.Value.SyncToDiskAsync().ConfigureAwait(false);
        }
    }

    public void DeleteFolders()
    {
        DeleteFolder(CachePath);
        DeleteFolder(ImageCacheFolder);
        DeleteFolder(TokenDirectoryPath);
    }

    private static void DeleteFolder(string path)
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