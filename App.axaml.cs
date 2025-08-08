using Aniki.Services.Aniki.Services;
using Aniki.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Markup.Xaml;
using Velopack;
using Velopack.Sources;

namespace Aniki;

public partial class App : Application
{
    private EpisodeNotificationService? _notificationService;

    public override void Initialize()
    {
        TokenService.Init();
        SaveService.Init();

        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new LoginWindow();

            CheckForUpdates();

            WindowNotificationManager notificationManager = new(desktop.MainWindow)
            {
                Position = NotificationPosition.TopRight,
                MaxItems = 3
            };

            _notificationService = new(notificationManager);
            _notificationService.Start();

            desktop.Exit += (_, _) => _notificationService.Stop();
        }

        AppDomain.CurrentDomain.UnhandledException += (s, e) => 
            Log.Fatal(e.ExceptionObject as Exception, "Unhandled exception");
        
        base.OnFrameworkInitializationCompleted();
    }

    private async void CheckForUpdates()
    {
        try
        {
            UpdateManager mgr = new(new GithubSource("https://github.com/TrueTheos/Aniki", null, false));

            UpdateInfo? newVersion = await mgr.CheckForUpdatesAsync();

            if (newVersion != null)
            {
                await mgr.DownloadUpdatesAsync(newVersion);

                mgr.ApplyUpdatesAndRestart(newVersion);
            }
        }
        catch (Exception ex)
        {
            Log.Information($"Update check failed: {ex.Message}");
        }
    }
}