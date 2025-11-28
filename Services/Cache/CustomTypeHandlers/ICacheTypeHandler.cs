namespace Aniki.Services.CustomTypeHandlers;

public interface ICacheTypeHandler
{
    void Serialize(object value, Stream destination);
    object Deserialize(Stream source);
}