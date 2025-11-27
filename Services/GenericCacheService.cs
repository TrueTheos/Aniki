using System.Collections.Concurrent;
using System.Reflection;

namespace Aniki.Services;

[AttributeUsage(AttributeTargets.Property)]
public class CacheFieldAttribute : Attribute
{
    public object FieldId { get; }
    public CacheFieldAttribute(object fieldId) => FieldId = fieldId;
}

public class CachedEntity<TEntity, TFieldEnum> where TEntity : class, new() where TFieldEnum : Enum
{
    public TEntity Data { get; } = new();
    
    private readonly HashSet<TFieldEnum> _fetchedFields = new();
    private readonly object _lock = new();

    public bool IsFieldFetched(TFieldEnum field)
    {
        lock (_lock) return _fetchedFields.Contains(field);
    }

    public void MarkFetched(IEnumerable<TFieldEnum> fields)
    {
        lock (_lock)
        {
            foreach (var f in fields) _fetchedFields.Add(f);
        }
    }
    
    public TFieldEnum[] GetMissingFields(TFieldEnum[] requested)
    {
        lock (_lock)
        {
            return requested.Where(r => !_fetchedFields.Contains(r)).ToArray();
        }
    }
}

public delegate void FieldChangeHandler<TEntity>(TEntity updatedEntity); 

public class GenericCacheService<TKey, TEntity, TFieldEnum>
    where TKey : notnull
    where TEntity : class, new()
    where TFieldEnum : Enum
{
    private readonly ConcurrentDictionary<TKey, CachedEntity<TEntity, TFieldEnum>> _cache = new();
    private readonly Func<TKey, TFieldEnum[], Task<TEntity?>>? _fetchHandler;
    
    private readonly Dictionary<TFieldEnum, PropertyInfo> _propertyMap = new();

    private readonly ConcurrentDictionary<TKey, Dictionary<TFieldEnum, FieldChangeHandler<TEntity>>> _fieldSubscriptions = new();
    private readonly object _subscriptionLock = new();
    
    private readonly ConcurrentDictionary<Type, Dictionary<TFieldEnum, PropertyInfo>> _sourceTypeMaps = new();

    public GenericCacheService(Func<TKey, TFieldEnum[], Task<TEntity?>>? fetchHandler)
    {
        _fetchHandler = fetchHandler;
        MapProperties();
    }
    
    private Dictionary<TFieldEnum, PropertyInfo> GetPropertyMapForType(Type type)
    {
        return _sourceTypeMaps.GetOrAdd(type, t =>
        {
            var map = new Dictionary<TFieldEnum, PropertyInfo>();
            foreach (var prop in t.GetProperties())
            {
                var attr = prop.GetCustomAttribute<CacheFieldAttribute>();
                if (attr != null && attr.FieldId is TFieldEnum fieldEnum)
                {
                    map[fieldEnum] = prop;
                }
            }
            return map;
        });
    }

    private void MapProperties()
    {
        var properties = typeof(TEntity).GetProperties();
        foreach (var prop in properties)
        {
            var attr = prop.GetCustomAttribute<CacheFieldAttribute>();
            if (attr != null && attr.FieldId is TFieldEnum fieldEnum)
            {
                _propertyMap[fieldEnum] = prop;
            }
        }
    }
    
    public void SubscribeToFieldChange(TKey entityId, TFieldEnum field, FieldChangeHandler<TEntity> handler)
    {
        lock (_subscriptionLock)
        {
            var entityHandlers = _fieldSubscriptions.GetOrAdd(entityId, new Dictionary<TFieldEnum, FieldChangeHandler<TEntity>>());

            if (entityHandlers.TryGetValue(field, out var currentHandler))
            {
                entityHandlers[field] = currentHandler + handler;
            }
            else
            {
                entityHandlers[field] = handler;
            }
        }
    }

    public void UnsubscribeFromFieldChange(TKey entityId, TFieldEnum field, FieldChangeHandler<TEntity> handler)
    {
        lock (_subscriptionLock)
        {
            if (!_fieldSubscriptions.TryGetValue(entityId, out var entityHandlers))
                return;

            if (!entityHandlers.TryGetValue(field, out var currentHandler))
                return;

            var updatedHandler = currentHandler - handler;
            
            if (updatedHandler == null)
            {
                entityHandlers.Remove(field);
                
                if (entityHandlers.Count == 0)
                {
                    _fieldSubscriptions.TryRemove(entityId, out _);
                }
            }
            else
            {
                entityHandlers[field] = updatedHandler;
            }
        }
    }

    private void NotifyFieldChanged(TKey entityId, TFieldEnum field, TEntity updatedEntity)
    {
        FieldChangeHandler<TEntity>? handler = null;
        
        lock (_subscriptionLock)
        {
            if (_fieldSubscriptions.TryGetValue(entityId, out var entityHandlers))
            {
                if (entityHandlers.TryGetValue(field, out handler))
                { 
                    // Copy to avoid holding lock
                }
            }
        }

        handler?.Invoke(updatedEntity);
    }

    private CachedEntity<TEntity, TFieldEnum> GetEntry(TKey id)
    {
        return _cache.GetOrAdd(id, _ => new CachedEntity<TEntity, TFieldEnum>());
    }

    public void Update(TKey id, TEntity newData, params TFieldEnum[] fieldsFetched)
    {
        var entry = GetEntry(id);
        
        foreach (var field in fieldsFetched)
        {
            if (_propertyMap.TryGetValue(field, out var prop))
            {
                var newValue = prop.GetValue(newData);
                prop.SetValue(entry.Data, newValue);
                
                NotifyFieldChanged(id, field, entry.Data);
            }
        }
        
        entry.MarkFetched(fieldsFetched);
    }
    
    public void UpdatePartial<TSource>(TKey id, TSource source, params TFieldEnum[] fieldsFetched)
        where TSource : class
    {
        var entry = GetEntry(id);
        var sourcePropertyMap = GetPropertyMapForType(typeof(TSource));

        foreach (var field in fieldsFetched)
        {
            if (_propertyMap.TryGetValue(field, out var entityProp))
            {
                if (sourcePropertyMap.TryGetValue(field, out var sourceProp))
                {
                    var newValue = sourceProp.GetValue(source);
                    entityProp.SetValue(entry.Data, newValue);
                
                    NotifyFieldChanged(id, field, entry.Data);
                }
            }
        }
    
        entry.MarkFetched(fieldsFetched);
    }


    public async Task<TEntity> GetAsync(TKey id, params TFieldEnum[] fields)
    {
        var entry = GetEntry(id);
        var missing = entry.GetMissingFields(fields);

        if (missing.Length > 0 && _fetchHandler != null)
        {
            var fetchedData = await _fetchHandler(id, missing);
            if (fetchedData != null)
            {
                Update(id, fetchedData, missing);
            }
            else 
            {
                entry.MarkFetched(missing); 
            }
        }

        return entry.Data;
    }
}