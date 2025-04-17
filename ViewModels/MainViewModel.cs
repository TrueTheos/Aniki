using Aniki.Models;
using Aniki.Services;
using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Aniki.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IMalApiService _malApiService;

        private string _username;
        private Bitmap _profileImage;
        private bool _isLoading;
        private string _selectedFilter;
        private ObservableCollection<AnimeData> _animeList;
        private ICommand _refreshCommand;
        private ICommand _logoutCommand;

        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        public Bitmap ProfileImage
        {
            get => _profileImage;
            set => SetProperty(ref _profileImage, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

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

        public ObservableCollection<AnimeData> AnimeList
        {
            get => _animeList;
            set => SetProperty(ref _animeList, value);
        }

        public ICommand RefreshCommand => _refreshCommand ??= new RelayCommand(async () => await RefreshDataAsync());
        public ICommand LogoutCommand => _logoutCommand ??= new RelayCommand(Logout);

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

        public MainViewModel(IMalApiService malApiService)
        {
            _malApiService = malApiService;
            _animeList = new ObservableCollection<AnimeData>();
            _selectedFilter = "All";
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
                var userData = await _malApiService.GetUserDataAsync();
                Username = userData.Name;
                ProfileImage = await _malApiService.GetProfileImageAsync(userData.Id);
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
                AnimeList.Clear();

                var animeListData = await _malApiService.GetAnimeListAsync(filter);

                foreach (var anime in animeListData)
                {
                    AnimeList.Add(anime);
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

        private async Task RefreshDataAsync()
        {
            await LoadUserDataAsync();
            await LoadAnimeListAsync(_selectedFilter);
        }

        private void Logout()
        {
            LogoutRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
