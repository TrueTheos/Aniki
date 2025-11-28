using Avalonia.Media.Imaging;

namespace Aniki.Services;

public class ImageSaver(string path) : SaveEntity<Bitmap>(path)
{
    public override void Save(string fileName, Bitmap data)
    {
        string filePath = Path.Combine(_path, fileName);
        using (FileStream stream = new(filePath, FileMode.Create))
        {
            data.Save(stream);
        }
    }

    public override Bitmap? Read(string fileName, Bitmap? defaultValue = null)
    {
        string filePath = Path.Combine(_path, fileName);
        if (!File.Exists(filePath)) return defaultValue;

        try
        {
            return new Bitmap(filePath);
        }
        catch (Exception)
        {
            File.Delete(filePath);
            return defaultValue;
        }
    }
}