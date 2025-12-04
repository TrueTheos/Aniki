namespace Aniki.Services.Cache;

public class CachedEntity<TEntity, TFieldEnum> where TEntity : class, new() where TFieldEnum : Enum
{
    public TEntity Data { get; } = new();
    
    private readonly Dictionary<TFieldEnum, DateTime> _fieldExpirations = new();
    private readonly HashSet<TFieldEnum> _fetchedFields = new();
    private readonly object _lock = new();
    
    public bool IsFieldFetched(TFieldEnum field)
    {
        lock (_lock) return _fetchedFields.Contains(field);
    }

    public bool IsFieldExpired(TFieldEnum field, TimeSpan ttl)
    {
        lock (_lock)
        {
            if (!_fieldExpirations.TryGetValue(field, out DateTime expiry))
                return true;
            return DateTime.UtcNow > expiry;
        }
    }
    
    public void MarkFetched(IEnumerable<TFieldEnum> fields, TimeSpan timeToLive)
    {
        lock (_lock)
        {
            DateTime expiry = DateTime.UtcNow.Add(timeToLive);
            foreach (TFieldEnum field in fields)
            {
                _fetchedFields.Add(field);
                _fieldExpirations[field] = expiry;
            }
        }
    }
    
    public TFieldEnum[] GetMissingFields(TFieldEnum[] requested, TimeSpan timeToLive)
    {
        lock (_lock)
        {
            return requested.Where(r => !_fetchedFields.Contains(r) || IsFieldExpired(r, timeToLive)).ToArray();
        }
    }
    
    public DateTime? GetFieldExpiration(TFieldEnum field)
    {
        lock (_lock)
        {
            return _fieldExpirations.TryGetValue(field, out DateTime expiry) ? expiry : null;
        }
    }
}