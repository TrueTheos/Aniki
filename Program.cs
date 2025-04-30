using System;
using System.Threading.Tasks;
using Avalonia;
using Velopack;

namespace Aniki
{
    internal class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            //VelopackApp.Build().Run();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();
    }
}
