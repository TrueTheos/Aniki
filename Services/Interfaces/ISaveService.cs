using Aniki.Services.Cache;
using Aniki.Services.Parser;
using Aniki.Services.Save;

namespace Aniki.Services.Interfaces;

internal interface ISaveService
{
    public string DefaultEpisodesFolder { get; }
    public string EpisodesFolder { get; }
    public GenericCacheService<string, AnimeSeasonsMap, AnimeSeasonsMap.AnimeSeasonMapField> GetSeasonCache();
    public void SaveSettings(SettingsConfig config);
    public SettingsConfig? GetSettingsConfig();
    public void RegisterCache(ICacheService cache);
    public Task ClearAllCaches();
    public Task SaveAllCaches();
}