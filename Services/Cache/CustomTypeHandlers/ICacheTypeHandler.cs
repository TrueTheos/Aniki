namespace Aniki.Services.Cache.CustomTypeHandlers;

public interface ICacheTypeHandler
{
    void Serialize(object value, Stream destination);
    object Deserialize(Stream source);
}