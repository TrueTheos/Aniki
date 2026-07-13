namespace Aniki.Services.Cache.CustomTypeHandlers;

internal interface ICacheTypeHandler
{
    void Serialize(object value, Stream destination);
    object Deserialize(Stream source);
}