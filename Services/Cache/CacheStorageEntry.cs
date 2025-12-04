namespace Aniki.Services.Cache;

public class CacheStorageEntry<TEntity, TFieldEnum> where TFieldEnum : Enum
{
    public TEntity Data { get; set; } = default!;
    public Dictionary<string, DateTime> FieldExpirations { get; set; } = new();
    public HashSet<string> FetchedFields { get; set; } = new();
}