using Aniki.Services.Cache.CustomTypeHandlers;

namespace Aniki.Services.Cache;

public class CacheOptions
{
    public TimeSpan DefaultTimeToLive { get; init; } = TimeSpan.FromHours(8);
    public string? DiskCachePath { get; set; }
    public TimeSpan DiskSyncInterval { get; init; } = TimeSpan.FromMinutes(5);
    public bool EnableDiskCache { get; init; } = true;
    public Dictionary<Type, ICacheTypeHandler> CustomTypeHandlers { get; } = new();

    public void RegisterTypeHandler<T>(ICacheTypeHandler handler)
    {
        CustomTypeHandlers[typeof(T)] = handler;
    }
}