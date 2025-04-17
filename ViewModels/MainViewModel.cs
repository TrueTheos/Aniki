using Aniki.Models;
using Aniki.Services;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml.Linq;

namespace Aniki.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ObservableCollection<AnimeData> _animeList;

        [ObservableProperty]
        private string _username;

        [ObservableProperty]
        private Bitmap _profileImage;

        [ObservableProperty]
        private bool _isLoading;

        private string _selectedFilter;
        public string SelectedFilter
        {
            get => _selectedFilter;
            set
            {
                if (SetProperty(ref _selectedFilter, value))
                {
                    _ = LoadAnimeListAsync(value);
                }
            }
        }

        private AnimeData _selectedAnime;
        public AnimeData SelectedAnime
        {
            get => _selectedAnime;
            set
            {
                if (SetProperty(ref _selectedAnime, value))
                {
                    _ = LoadAnimeDetailsAsync(value);
                }
            }
        }

        [ObservableProperty]
        private AnimeDetails _animeDetails;

        public event EventHandler LogoutRequested;

        public List<string> FilterOptions { get; } = new List<string>
        {
            "All",
            "Currently Watching",
            "Completed",
            "On Hold",
            "Dropped",
            "Plan to Watch"
        };

        public MainViewModel() 
        {
            _animeList = new ObservableCollection<AnimeData>();
            _selectedFilter = "All";
        }

        private async Task LoadAnimeDetailsAsync(AnimeData animeData)
        {
            if (animeData?.Node?.Id != null)
            {
                AnimeDetails = await MalUtils.GetAnimeDetails(animeData.Node.Id);
            }
        }

        public async Task InitializeAsync()
        {
            await LoadUserDataAsync();
            await LoadAnimeListAsync(_selectedFilter);
        }

        private async Task LoadUserDataAsync()
        {
            try
            {
                IsLoading = true;
                var userData = await MalUtils.GetUserDataAsync();
                Username = userData.Name;
                ProfileImage = await MalUtils.GetUserPicture();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading user data: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadAnimeListAsync(string filter)
        {
            try
            {
                IsLoading = true;
                _animeList.Clear();

                var animeListData = await MalUtils.LoadAnimeList(filter);

                foreach (var anime in animeListData)
                {
                    _animeList.Add(anime);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading anime list: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task Refresh()
        {
            await LoadUserDataAsync();
            await LoadAnimeListAsync(_selectedFilter);
        }

        [RelayCommand]
        private void Logout()
        {
            LogoutRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
