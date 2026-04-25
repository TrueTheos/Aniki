using System.Collections.ObjectModel;
using Aniki.Services.Interfaces;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Win32;

namespace Aniki.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    public bool AutoStart
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                ChangeAutoStart(value);
            }
        }
    }

    [ObservableProperty]
    private bool _startMinimized;

    [ObservableProperty]
    private bool _enableDiscordPresence = true;

    [ObservableProperty]
    private string? _episodesFolder;

    [ObservableProperty]
    private long _cacheSize;

    [ObservableProperty]
    private bool _isClearingCache;

    private readonly ISaveService _saveService;
    private readonly IVideoPlayerService _videoPlayerService;
    private readonly IDiscordService _discordService;

    public ObservableCollection<VideoPlayerOption> AvailablePlayers => _videoPlayerService.AvailablePlayers;

    public VideoPlayerOption? SelectedPlayer
    {
        get => _videoPlayerService.SelectedPlayer;
        set
        {
            if (_videoPlayerService.SelectedPlayer != value)
            {
                _videoPlayerService.SelectedPlayer = value;
                OnPropertyChanged();
            }
        }
    }

    public SettingsViewModel(ISaveService saveService, IVideoPlayerService videoPlayerService, IDiscordService discordService)
    {
        _saveService = saveService;
        _videoPlayerService = videoPlayerService;
        _discordService = discordService;
        
        LoadSettings();
    }

    public void LoadSettings()
    {
        SettingsConfig? config = _saveService.GetSettingsConfig();

        if (config == null)
        {
            AutoStart = false;
            StartMinimized = false;
            EnableDiscordPresence = true;
            EpisodesFolder = _saveService.DefaultEpisodesFolder;
        }
        else
        {
            AutoStart = config.AutoStart;
            StartMinimized = config.StartMinimized;
            EnableDiscordPresence = config.EnableDiscordPresence;
            EpisodesFolder = config.EpisodesFolder;

            if (!string.IsNullOrEmpty(config.PreferredVideoPlayerPath))
            {
                VideoPlayerOption? match = AvailablePlayers
                    .FirstOrDefault(p => p.ExecutablePath == config.PreferredVideoPlayerPath);
                if (match != null)
                    SelectedPlayer = match;
            }
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

    private void ChangeAutoStart(bool newValue)
    {
        if (OperatingSystem.IsWindows())
        {
            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
            {
                if (newValue)
                {
                    string exePath = Environment.ProcessPath ?? "";
                    key?.SetValue("Aniki", $"{exePath} --background");
                }
                else
                {
                    key?.DeleteValue("Aniki", false);
                }
            }
        }
    }

    [RelayCommand]
    private async Task ClearCache()
    {
        IsClearingCache = true;
        try
        {
            await _saveService.FlushAllCaches();
        }
        finally
        {
            IsClearingCache = false;
        }
    }

    [RelayCommand]
    private void Save()
    {
        SettingsConfig config = new()
        {
            AutoStart = AutoStart,
            StartMinimized = StartMinimized,
            EnableDiscordPresence = EnableDiscordPresence,
            EpisodesFolder = EpisodesFolder,
            PreferredVideoPlayerPath = SelectedPlayer?.ExecutablePath
        };

        _saveService.SaveSettings(config);

        _discordService.SetEnabled(EnableDiscordPresence);

        WeakReferenceMessenger.Default.Send(new SettingsChangedMessage());
    }
}

public class SettingsConfig
{
    public bool AutoStart;
    public bool StartMinimized;
    public bool EnableDiscordPresence = true;
    public string? EpisodesFolder;
    public string? PreferredVideoPlayerPath;
}

public class SettingsChangedMessage { }