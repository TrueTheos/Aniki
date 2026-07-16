using System.Diagnostics;
using Avalonia;
using Velopack;

namespace Aniki;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp builder = VelopackApp.Build();
        if (OperatingSystem.IsWindows())
            builder = builder.OnAfterUpdateFastCallback(_ => ClearCachesAfterUpdate());
        builder.Run();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    private static void ClearCachesAfterUpdate()
    {
        try
        {
            string root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Aniki");

            foreach (string name in new[] { "cache", "ImageCache" })
            {
                string path = Path.Combine(root, name);
                if (Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
            }

            Debug.WriteLine("Cleared caches after update.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Post-update cache clear failed: {ex.Message}");
        }
    }
}
