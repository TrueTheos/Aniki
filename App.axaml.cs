using Aniki.Misc;
using Aniki.Services.Anime;
using Aniki.Services.Interfaces;
using Aniki.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Velopack;
using Velopack.Sources;

namespace Aniki;

public partial class App : Application
{
    public override void Initialize()
    {
        DependencyInjection.Instance.BuildServiceProvider();
        AvaloniaXamlLoader.Load(this);
    }
    
    public void Reset()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;
        
        var oldWindow = desktop.MainWindow;

        AnimeService.IsLoggedIn = false;
        DependencyInjection.Instance.Logout();
        
        desktop.MainWindow = new LoginWindow();
        desktop.MainWindow.Show();
        
        oldWindow?.Close();
        
    }

    public override void OnFrameworkInitializationCompleted()
    {
        BindingPlugins.DataValidators.RemoveAt(0);
        
        DependencyInjection.Instance.ServiceProvider!.GetService<ITokenService>()?.Init();
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownRequested += OnShutdownRequested;
            
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
    
    private async void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        e.Cancel = true;
        
        try
        {
            await DependencyInjection.Instance.ServiceProvider!.GetRequiredService<ISaveService>().FlushAllCaches();
            
            if (sender is IClassicDesktopStyleApplicationLifetime lifetime)
            {
                lifetime.ShutdownRequested -= OnShutdownRequested;
                lifetime.Shutdown();
            }
        }
        catch
        {
            // ignored
        }
    }
}