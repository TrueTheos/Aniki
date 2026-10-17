namespace Aniki.Services.Cache;

internal sealed class CachedEntity<TEntity, TFieldEnum> where TEntity : class, new() where TFieldEnum : Enum
{
    public TEntity Data { get; } = new();
    
    private readonly Dictionary<TFieldEnum, DateTime> _fieldExpirations = new();
    private readonly Lock _lock = new();
    
    public void MarkFetched(IEnumerable<TFieldEnum> fields, TimeSpan timeToLive)
    {
        lock (_lock)
        {
            DateTime expiry = DateTime.UtcNow.Add(timeToLive);
            foreach (TFieldEnum field in fields)
            {
                _fieldExpirations[field] = expiry;
            }
        }
    }
    
    public TFieldEnum[] GetMissingFields(TFieldEnum[] requested)
    {
        lock (_lock)
        {
            DateTime now = DateTime.UtcNow;
            return requested.Where(r => !_fieldExpirations.TryGetValue(r, out DateTime expiry) || now > expiry).ToArray();
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