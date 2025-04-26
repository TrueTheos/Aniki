using Avalonia.Controls;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using System.Linq;
using Aniki.Services;

namespace Aniki.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        [ObservableProperty]
        private bool _autoStart;

        [ObservableProperty]
        private string _episodesFolder;

        public SettingsViewModel()
        {
            LoadSettings();
        }

        private void LoadSettings()
        {
            SettingsConfig config = SaveService.GetSettingsConfig();

            if(config == null)
            {
                AutoStart = false;
                EpisodesFolder = SaveService.DefaultEpisodesFolder;
            }
            else
            {
                AutoStart = config.AutoStart;
                EpisodesFolder = config.EpisodesFolder;
            }
        }

        [RelayCommand]
        private async Task BrowseEpisodesFolder()
        {
            var dlg = new OpenFolderDialog { Title = "Select Download Folder", Directory = _episodesFolder };
            var window = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Windows
                .FirstOrDefault(w => w.DataContext == this) ??
                (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

            if (window != null)
            {
                var result = await dlg.ShowAsync(window);
                if (!string.IsNullOrEmpty(result))
                    EpisodesFolder = result;
            }
        }

        [RelayCommand]
        private void Save()
        {
            var config = new SettingsConfig
            {
                AutoStart = AutoStart,
                EpisodesFolder = EpisodesFolder,
            };

            SaveService.SaveSettings(config);
            // TODO: Handle Windows registry for AutoStart if necessary
        }
    }

    public class SettingsConfig
    {
        public bool AutoStart { get; set; }
        public string EpisodesFolder { get; set; }
    }
}