namespace Aniki.Services.Cache;

[AttributeUsage(AttributeTargets.Property)]
internal sealed class CacheFieldAttribute(object fieldId, bool cacheInMemory = false) : Attribute
{
    public object FieldId { get; } = fieldId;
    public bool CacheInMemory { get; } = cacheInMemory;
}