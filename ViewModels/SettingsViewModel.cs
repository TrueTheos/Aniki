using Aniki.Services.Interfaces;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Win32;

namespace Aniki.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private bool _autoStart;
    public bool AutoStart
    {
        get => _autoStart;
        set
        {
            if (SetProperty(ref _autoStart, value))
            {
                ChangeAutoStart(value);
            }
        }
    }

    [ObservableProperty]
    private string? _episodesFolder;

    private bool _notifyAboutEpisodes;
    public bool NotifyAboutEpisodes
    {
        get => _notifyAboutEpisodes;
        set
        {
            if (SetProperty(ref _notifyAboutEpisodes, value))
            {
                ChangeNotifyAboutEpisodes(value);
            }
        }
    }

    [ObservableProperty]
    private long _cacheSize;

    private readonly ISaveService _saveService;
    private readonly CacheManager _cacheManager;

    public SettingsViewModel(ISaveService saveService)
    {
        _saveService = saveService;
        
        LoadSettings();
        _cacheManager = _saveService.ImageCache!;
        UpdateCacheSize();
    }

    private void UpdateCacheSize()
    {
        CacheSize = _cacheManager.GetCacheSize();
    }

    [RelayCommand]
    private void ClearCache()
    {
        _cacheManager.ClearCache();
        UpdateCacheSize();
    }

    private void LoadSettings()
    {
        SettingsConfig? config = _saveService.GetSettingsConfig();

        if(config == null)
        {
            AutoStart = false;
            EpisodesFolder = _saveService.DefaultEpisodesFolder;
        }
        else
        {
            AutoStart = config.AutoStart;
            EpisodesFolder = config.EpisodesFolder;
            NotifyAboutEpisodes = config.NotifyAboutEpisodes;
        }
    }

    [RelayCommand]
    [Obsolete]
    private async Task BrowseEpisodesFolder()
    {
        OpenFolderDialog dlg = new() { Title = "Select Download Folder", Directory = EpisodesFolder };
        Window? window = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Windows
                         .FirstOrDefault(w => w.DataContext == this) ??
                         (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

        if (window != null)
        {
            string? result = await dlg.ShowAsync(window);
            if (!string.IsNullOrEmpty(result))
                EpisodesFolder = result;
        }
    }

    private void ChangeNotifyAboutEpisodes(bool newValue)
    {

    }

    private void ChangeAutoStart(bool newValue)
    {
        if (OperatingSystem.IsWindows())
        {
            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
            {
                if (newValue)
                {
                    string exePath = Environment.ProcessPath ?? "";
                    key?.SetValue("Avalonia", $"{exePath} --background");
                }
                else
                {
                    key?.DeleteValue("Avalonia", false);
                }
            }
        }
    }

    [RelayCommand]
    private void Save()
    {
        SettingsConfig config = new()
        {
            AutoStart = AutoStart,
            EpisodesFolder = EpisodesFolder,
            NotifyAboutEpisodes = NotifyAboutEpisodes
        };

        _saveService.SaveSettings(config);
        
        WeakReferenceMessenger.Default.Send(new SettingsChangedMessage());
    }
}

public class SettingsConfig
{
    public bool AutoStart { get; set; }
    public string? EpisodesFolder { get; set; }
    public bool NotifyAboutEpisodes { get; set; }
}

public class SettingsChangedMessage { }