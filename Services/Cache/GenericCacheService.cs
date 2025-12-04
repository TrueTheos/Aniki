using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Aniki.Services.Cache.CustomTypeHandlers;

namespace Aniki.Services.Cache;

public delegate void FieldChangeHandler<TEntity>(TEntity updatedEntity);

public interface ICacheService
{
    public Task FlushAsync();
    public void ClearMemory();
    public Task ClearAllAsync();
    public void Dispose();
}

public class GenericCacheService<TKey, TEntity, TFieldEnum> : ICacheService
    where TKey : notnull
    where TEntity : class, new()
    where TFieldEnum : Enum
{
    private readonly ConcurrentDictionary<TKey, CachedEntity<TEntity, TFieldEnum>> _cache = new();
    private readonly Func<TKey, TFieldEnum[], Task<TEntity?>>? _fetchHandler;
    private readonly CacheOptions _options;
    
    private readonly Dictionary<TFieldEnum, PropertyInfo> _propertyMap = new();
    private readonly ConcurrentDictionary<TKey, Dictionary<TFieldEnum, FieldChangeHandler<TEntity>>> _fieldSubscriptions = new();
    private readonly object _subscriptionLock = new();
    private readonly ConcurrentDictionary<Type, Dictionary<TFieldEnum, PropertyInfo>> _sourceTypeMaps = new();

    private readonly Timer? _diskSyncTimer;
    private readonly SemaphoreSlim _diskWriteLock = new(1, 1);
    private readonly HashSet<TKey> _dirtyKeys = new();
    private readonly object _dirtyKeysLock = new();
    
    private readonly HashSet<TFieldEnum> _memoryOnlyFields = new();
    
    private class StoredCacheEntry
    {
        public string? Key { get; set; }
        public StoredEntityData? Entry { get; set; }
    }

    private class StoredEntityData
    {
        public JsonElement Data { get; set; }
        public Dictionary<string, DateTime> FieldExpirations { get; set; } = new();
        public HashSet<string> FetchedFields { get; set; } = new();
    }
    
    public GenericCacheService(Func<TKey, TFieldEnum[], Task<TEntity?>>? fetchHandler, CacheOptions? options = null)
    {
        _fetchHandler = fetchHandler;
        _options = options ?? new CacheOptions();
        MapProperties();
        
        if (_options.EnableDiskCache)
        {
            EnsureCacheDirectory();
            _ = LoadFromDiskAsync();
            
            _diskSyncTimer = new Timer(async void (_) =>
            {
                try
                {
                    await SyncToDiskAsync();
                }
                catch (Exception e)
                {
                    Log.Error($"Failed to save to disk {e}");
                }
            }, null, _options.DiskSyncInterval, _options.DiskSyncInterval);
        }
    }

    #region Disk Functionality

    private void EnsureCacheDirectory()
    {
        if (string.IsNullOrEmpty(_options.DiskCachePath))
        {
            _options.DiskCachePath = Path.Combine(Path.GetTempPath(), "cache", typeof(TEntity).Name);
        }

        Directory.CreateDirectory(_options.DiskCachePath);
    }

    private string GetCacheFilePath(TKey key)
    {
        string safeFileName = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(key.ToString() ?? "")).Replace("/", "_").Replace("+", "-");
        return Path.Combine(_options.DiskCachePath!, $"{safeFileName}.json");
    }
    
    private async Task LoadFromDiskAsync()
    {
        if (string.IsNullOrEmpty(_options.DiskCachePath) || !Directory.Exists(_options.DiskCachePath))
            return;

        string[] files = Directory.GetFiles(_options.DiskCachePath, "*.json");
        
        foreach (string file in files)
        {
            //skipping custom type handlers
            if (file.Contains("_") && file.EndsWith(".bin")) continue;
            
            try
            {
                string json = await File.ReadAllTextAsync(file);
                StoredCacheEntry? stored = JsonSerializer.Deserialize<StoredCacheEntry>(json);
                
                if (stored?.Key == null || stored.Entry == null)
                    continue;

                TKey key = (TKey)Convert.ChangeType(stored.Key, typeof(TKey));
                CachedEntity<TEntity, TFieldEnum> entry = new();
                
                foreach (KeyValuePair<TFieldEnum, PropertyInfo> prop in _propertyMap)
                {
                    if (!stored.Entry.Data.TryGetProperty(prop.Key.ToString(), out JsonElement value)) continue;
                    
                    Type propType = prop.Value.PropertyType;

                    if (_options.CustomTypeHandlers.TryGetValue(propType, out ICacheTypeHandler? handler))
                    {
                        string? sidecarFileName = value.GetString();

                        if (sidecarFileName == null || !sidecarFileName.StartsWith("file://")) continue;
                        string actualFileName = sidecarFileName.Replace("file://", "");
                        string sidecarPath = Path.Combine(_options.DiskCachePath!, actualFileName);

                        if (!File.Exists(sidecarPath)) continue;
                        using (FileStream fs = new(sidecarPath, FileMode.Open, FileAccess.Read))
                        {
                            object loadedObj = handler.Deserialize(fs);
                            prop.Value.SetValue(entry.Data, loadedObj);
                        }
                    }
                    else
                    {
                        object? propValue = JsonSerializer.Deserialize(value.GetRawText(), propType);
                        prop.Value.SetValue(entry.Data, propValue);
                    }
                }

                List<TFieldEnum> fieldsToRestore = new();
                foreach (string fieldName in stored.Entry.FetchedFields)
                {
                    if (Enum.TryParse(typeof(TFieldEnum), fieldName, out object? fieldObj) && fieldObj is TFieldEnum field)
                    {
                        if (stored.Entry.FieldExpirations.TryGetValue(fieldName, out DateTime expiry))
                        {
                            if (DateTime.UtcNow <= expiry)
                            {
                                fieldsToRestore.Add(field);
                            }
                        }
                    }
                }

                if (fieldsToRestore.Any())
                {
                    entry.MarkFetched(fieldsToRestore, TimeSpan.Zero);
                    _cache.TryAdd(key, entry);
                }
            }
            catch{ /*Corrupted file*/ }
        }
    }

    private async Task SyncToDiskAsync()
    {
        if (!_options.EnableDiskCache)
            return;

        List<TKey> keysToSync;
        lock (_dirtyKeysLock)
        {
            keysToSync = _dirtyKeys.ToList();
            _dirtyKeys.Clear();
        }

        await _diskWriteLock.WaitAsync();
        try
        {
            foreach (TKey key in keysToSync)
            {
                if (_cache.TryGetValue(key, out CachedEntity<TEntity, TFieldEnum>? entry))
                {
                    await SaveEntryToDiskAsync(key, entry);
                }
            }
        }
        finally
        {
            _diskWriteLock.Release();
        }
    }

    private async Task SaveEntryToDiskAsync(TKey key, CachedEntity<TEntity, TFieldEnum> entry)
    {
        string baseFilePath = GetCacheFilePath(key);
        string safeKey = Path.GetFileNameWithoutExtension(baseFilePath);
        
        Dictionary<string, object?> dataDict = new();
        
        foreach (KeyValuePair<TFieldEnum, PropertyInfo> prop in _propertyMap)
        {
            if (_memoryOnlyFields.Contains(prop.Key))
                continue;
            
            object? val = prop.Value.GetValue(entry.Data);
            if (val == null) continue;

            Type propType = prop.Value.PropertyType;
            
            if (_options.CustomTypeHandlers.TryGetValue(propType, out ICacheTypeHandler? handler))
            {
                string sidecarFileName = $"{safeKey}_{prop.Key}.bin";
                string sidecarPath = Path.Combine(_options.DiskCachePath!, sidecarFileName);

                using (FileStream fs = new(sidecarPath, FileMode.Create))
                {
                    handler.Serialize(val, fs);
                }

                dataDict[prop.Key.ToString()] = $"file://{sidecarFileName}";
            }
            else
            {
                dataDict[prop.Key.ToString()] = val;
            }
        }

        Dictionary<string, DateTime> fieldExpirations = new();
        HashSet<string> fetchedFields = new();
        
        foreach (TFieldEnum field in Enum.GetValues(typeof(TFieldEnum)))
        {
            if (_memoryOnlyFields.Contains(field))
                continue;
            
            if (entry.IsFieldFetched(field))
            {
                fetchedFields.Add(field.ToString());
                DateTime? expiry = entry.GetFieldExpiration(field);
                if (expiry.HasValue)
                {
                    fieldExpirations[field.ToString()] = expiry.Value;
                }
            }
        }

        StoredCacheEntry stored = new()
        {
            Key = key.ToString(),
            Entry = new StoredEntityData
            {
                Data = JsonSerializer.SerializeToElement(dataDict),
                FieldExpirations = fieldExpirations,
                FetchedFields = fetchedFields
            }
        };

        string json = JsonSerializer.Serialize(stored, new JsonSerializerOptions 
        { 
            WriteIndented = false 
        });
        
        await File.WriteAllTextAsync(baseFilePath, json);
    }

    private void MarkDirty(TKey key)
    {
        if (!_options.EnableDiskCache)
            return;

        lock (_dirtyKeysLock)
        {
            _dirtyKeys.Add(key);
        }
    }

    #endregion Disk Functionality
    
    private Dictionary<TFieldEnum, PropertyInfo> GetPropertyMapForType(Type type)
    {
        return _sourceTypeMaps.GetOrAdd(type, t =>
        {
            Dictionary<TFieldEnum, PropertyInfo> map = new();
            foreach (PropertyInfo prop in t.GetProperties())
            {
                CacheFieldAttribute? attribute = prop.GetCustomAttribute<CacheFieldAttribute>();
                if (attribute != null && attribute.FieldId is TFieldEnum fieldEnum)
                {
                    map[fieldEnum] = prop;
                }
            }
            return map;
        });
    }

    private void MapProperties()
    {
        PropertyInfo[] properties = typeof(TEntity).GetProperties();
        foreach (PropertyInfo prop in properties)
        {
            CacheFieldAttribute? attribute = prop.GetCustomAttribute<CacheFieldAttribute>();
            if (attribute != null && attribute.FieldId is TFieldEnum fieldEnum)
            {
                _propertyMap[fieldEnum] = prop;
                
                if (attribute.CacheInMemory)
                {
                    _memoryOnlyFields.Add(fieldEnum);
                }
            }
        }
    }
    
    public void SubscribeToFieldChange(TKey entityId, FieldChangeHandler<TEntity> handler, params TFieldEnum[] fields)
    {
        lock (_subscriptionLock)
        {
            Dictionary<TFieldEnum, FieldChangeHandler<TEntity>> entityHandlers = _fieldSubscriptions.GetOrAdd(entityId, new Dictionary<TFieldEnum, FieldChangeHandler<TEntity>>());

            foreach (TFieldEnum field in fields)
            {
                if (entityHandlers.TryGetValue(field, out FieldChangeHandler<TEntity>? currentHandler))
                {
                    entityHandlers[field] = currentHandler + handler;
                }
                else
                {
                    entityHandlers[field] = handler;
                }
            }
        }
    }

    public void UnsubscribeFromFieldChange(TKey entityId, FieldChangeHandler<TEntity> handler, params TFieldEnum[] fields)
    {
        lock (_subscriptionLock)
        {
            if (!_fieldSubscriptions.TryGetValue(entityId, out Dictionary<TFieldEnum, FieldChangeHandler<TEntity>>? entityHandlers))
                return;

            foreach (TFieldEnum field in fields)
            {
                if (!entityHandlers.TryGetValue(field, out FieldChangeHandler<TEntity>? currentHandler))
                    return;

                FieldChangeHandler<TEntity>? updatedHandler = currentHandler - handler;
            
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
    }

    private void NotifyFieldChanged(TKey entityId, TFieldEnum field, TEntity updatedEntity)
    {
        FieldChangeHandler<TEntity>? handler = null;
        
        lock (_subscriptionLock)
        {
            if (_fieldSubscriptions.TryGetValue(entityId, out Dictionary<TFieldEnum, FieldChangeHandler<TEntity>>? entityHandlers))
            {
                if (entityHandlers.TryGetValue(field, out handler))
                { 
                    // Copy to avoid holding lock
                }
            }
        }

        handler?.Invoke(updatedEntity);
    }

    public CachedEntity<TEntity, TFieldEnum> GetOrCreateEntry(TKey id)
    {
        return _cache.GetOrAdd(id, _ => new CachedEntity<TEntity, TFieldEnum>());
    }

    /*public bool TryGetEntry(TKey id, [NotNullWhen(true)] out CachedEntity<TEntity, TFieldEnum>? entry)
    {
        return _cache.TryGetValue(id, out entry);
    }*/
    
    public void Update(TKey id, TEntity newData, params TFieldEnum[] fieldsFetched)
    {
        CachedEntity<TEntity, TFieldEnum> entry = GetOrCreateEntry(id);
        
        foreach (TFieldEnum field in fieldsFetched)
        {
            if (_propertyMap.TryGetValue(field, out PropertyInfo? prop))
            {
                object? newValue = prop.GetValue(newData);
                object? oldValue = prop.GetValue(entry.Data);
                
                if (oldValue is IDisposable disposableOld && !ReferenceEquals(oldValue, newValue))
                {
                    disposableOld.Dispose();
                }

                prop.SetValue(entry.Data, newValue);
            
                NotifyFieldChanged(id, field, entry.Data);
            }
        }
        
        entry.MarkFetched(fieldsFetched, _options.DefaultTimeToLive);
        MarkDirty(id);
    }
    
    public void UpdatePartial<TSource>(TKey id, TSource source, params TFieldEnum[] fieldsFetched)
        where TSource : class
    {
        CachedEntity<TEntity, TFieldEnum> entry = GetOrCreateEntry(id);
        Dictionary<TFieldEnum, PropertyInfo> sourcePropertyMap = GetPropertyMapForType(typeof(TSource));

        foreach (TFieldEnum field in fieldsFetched)
        {
            if (!_propertyMap.TryGetValue(field, out PropertyInfo? entityProp)) continue;
            if (!sourcePropertyMap.TryGetValue(field, out PropertyInfo? sourceProp)) continue;
            
            object? newValue = sourceProp.GetValue(source);
            object? oldValue = entityProp.GetValue(entry.Data);
            
            if (oldValue is IDisposable disposableOld && !ReferenceEquals(oldValue, newValue))
            {
                disposableOld.Dispose();
            }

            entityProp.SetValue(entry.Data, newValue);
            
            NotifyFieldChanged(id, field, entry.Data);
        }
    
        entry.MarkFetched(fieldsFetched, _options.DefaultTimeToLive);
        MarkDirty(id);
    }

    public async Task<TEntity> GetOrFetchFieldsAsync(TKey id, bool forceFetch = false, params TFieldEnum[] fields)
    {
        CachedEntity<TEntity, TFieldEnum> entry = GetOrCreateEntry(id);

        TFieldEnum[] missing = forceFetch ? fields : entry.GetMissingFields(fields, _options.DefaultTimeToLive);

        if (missing.Length > 0 && _fetchHandler != null)
        {
            TEntity? fetchedData = await _fetchHandler(id, missing);
            if (fetchedData != null)
            {
                Update(id, fetchedData, missing);
            }
            else 
            {
                entry.MarkFetched(missing, _options.DefaultTimeToLive); 
                MarkDirty(id);
            }
        }

        return entry.Data;
    }

    public TEntity? GetWithoutFetching(TKey id)
    {
        if (_cache.ContainsKey(id)) return _cache[id].Data;
        return null;
    }
    
    public async Task FlushAsync()
    {
        await SyncToDiskAsync();
    }

    public void ClearMemory()
    {
        foreach (CachedEntity<TEntity, TFieldEnum> item in _cache.Values)
        {
            foreach (PropertyInfo prop in _propertyMap.Values)
            {
                if (typeof(IDisposable).IsAssignableFrom(prop.PropertyType))
                {
                    IDisposable? val = prop.GetValue(item.Data) as IDisposable;
                    val?.Dispose();
                }
            }
        }
        _cache.Clear();
    }

    public async Task ClearAllAsync()
    {
        ClearMemory();
        
        if (_options.EnableDiskCache && !string.IsNullOrEmpty(_options.DiskCachePath))
        {
            await _diskWriteLock.WaitAsync();
            try
            {
                if (Directory.Exists(_options.DiskCachePath))
                {
                    Directory.Delete(_options.DiskCachePath, true);
                    Directory.CreateDirectory(_options.DiskCachePath);
                }
            }
            finally
            {
                _diskWriteLock.Release();
            }
        }
    }

    public void Dispose()
    {
        _diskSyncTimer?.Dispose();
        SyncToDiskAsync().GetAwaiter().GetResult();
        _diskWriteLock.Dispose();
    }
}