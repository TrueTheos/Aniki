namespace Aniki.Services;

[AttributeUsage(AttributeTargets.Property)]
public class CacheFieldAttribute : Attribute
{
    public object FieldId { get; }
    public CacheFieldAttribute(object fieldId) => FieldId = fieldId;
}