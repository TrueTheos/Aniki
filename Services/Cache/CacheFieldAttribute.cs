namespace Aniki.Services;

[AttributeUsage(AttributeTargets.Property)]
public class CacheFieldAttribute(object fieldId, bool cacheInMemory = false) : Attribute
{
    public object FieldId { get; } = fieldId;
    public bool CacheInMemory { get; } = cacheInMemory;
}