namespace Aniki.Services.Cache;

internal sealed class CacheOptions
{
    public TimeSpan DefaultTimeToLive { get; init; } = TimeSpan.FromHours(8);
    public string? DiskCachePath { get; set; }
    public TimeSpan DiskSyncInterval { get; init; } = TimeSpan.FromMinutes(5);
    public bool EnableDiskCache { get; init; } = true;
}