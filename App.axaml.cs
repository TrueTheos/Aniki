using Aniki.Misc;
using Aniki.Services.Interfaces;
using Aniki.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Velopack;
using Velopack.Sources;

namespace Aniki;

public partial class App : Application
{
    private ServiceProvider _serviceProvider = null!;
    
    public override void Initialize()
    {
        var collection = new ServiceCollection();
        collection.AddCommonServices();
        _serviceProvider = collection.BuildServiceProvider();

        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        BindingPlugins.DataValidators.RemoveAt(0);
        
        _serviceProvider.GetService<ITokenService>()?.Init();
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = _serviceProvider.GetRequiredService<LoginViewModel>();

            desktop.MainWindow = new LoginWindow();

            CheckForUpdates();
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
    
    public static ServiceProvider ServiceProvider => ((App)Current!)._serviceProvider;
}