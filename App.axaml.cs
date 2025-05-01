using Aniki.Services;
using Aniki.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System;
using Velopack;
using Velopack.Sources;

namespace Aniki
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            TokenService.Init();
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new LoginWindow();

               CheckForUpdates();
            }

            base.OnFrameworkInitializationCompleted();
        }

        private async void CheckForUpdates()
        {
            try
            {
                var mgr = new UpdateManager(new GithubSource("https://github.com/TrueTheos/Aniki", null, false));

                var newVersion = await mgr.CheckForUpdatesAsync();

                if (newVersion != null)
                {
                    await mgr.DownloadUpdatesAsync(newVersion);

                    mgr.ApplyUpdatesAndRestart(newVersion);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Update check failed: {ex.Message}");
            }
        }
    }
}