using Avalonia;
using Velopack;

namespace Aniki;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        string path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Aniki", "logs", "app-.txt");
        VelopackApp.Build().Run();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}