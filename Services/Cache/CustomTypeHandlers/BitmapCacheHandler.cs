namespace Aniki.Services.Cache.CustomTypeHandlers;

public class BitmapCacheHandler : ICacheTypeHandler
{
    public void Serialize(object value, Stream destination)
    {
        if (value is Avalonia.Media.Imaging.Bitmap bmp)
        {
            using (destination)
            {
                bmp.Save(destination);
            }
        }
    }

    public object Deserialize(Stream source)
    {
        return new Avalonia.Media.Imaging.Bitmap(source);
    }
}