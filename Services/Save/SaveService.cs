using System.Collections.Concurrent;
using Aniki.Services.Anime;
using Aniki.Services.Auth;
using Aniki.Services.Cache;
using Aniki.Services.Interfaces;
using Aniki.Services.Parser;

namespace Aniki.Services.Save;

internal sealed class SaveService : ISaveService
{
    private static readonly string MainDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Aniki");

    public static readonly string CachePath = Path.Combine(MainDirectory, "cache");
    public static readonly string TokenDirectoryPath = Path.Combine(MainDirectory, "tokens");

    public string DefaultEpisodesFolder => Path.Combine(MainDirectory, "Episodes");
    public string EpisodesFolder => GetSettingsConfig()?.EpisodesFolder ?? DefaultEpisodesFolder;

    private readonly SaveEntity<SettingsConfig> _settingsSaver = new(MainDirectory);

    private readonly Dictionary<ILoginProvider.ProviderType,
        GenericCacheService<string, AnimeSeasonsMap, AnimeSeasonsMap.AnimeSeasonMapField>> _seasonsCache = new();

    private readonly ConcurrentBag<ICacheService> _caches = [];

    public SaveService()
    {
        Directory.CreateDirectory(MainDirectory);
        Directory.CreateDirectory(DefaultEpisodesFolder);

        foreach (ILoginProvider.ProviderType providerType in Enum.GetValues<ILoginProvider.ProviderType>())
        {
            CacheOptions options = new()
            {
                DiskCachePath = $"{CachePath}/SeasonMaps/{providerType}",
            };
            GenericCacheService<string, AnimeSeasonsMap, AnimeSeasonsMap.AnimeSeasonMapField> cache = new(null, options);
            _caches.Add(cache);
            _seasonsCache[providerType] = cache;
        }
    }

    public GenericCacheService<string, AnimeSeasonsMap, AnimeSeasonsMap.AnimeSeasonMapField> GetSeasonCache() => _seasonsCache[AnimeService.CurrentProviderType];

    public void SaveSettings(SettingsConfig config) => _settingsSaver.Save("config.json", config);
    public SettingsConfig? GetSettingsConfig() => _settingsSaver.Read("config.json");

    public void RegisterCache(ICacheService cache)
    {
        _caches.Add(cache);
    }

    public async Task ClearAllCaches()
    {
        foreach (ICacheService cache in _caches)
        {
            await cache.ClearAllAsync().ConfigureAwait(false);
        }
    }

    public async Task SaveAllCaches()
    {
        foreach (ICacheService cache in _caches)
        {
            await cache.SyncToDiskAsync().ConfigureAwait(false);
        }
    }
}
