using Aniki.Models;
using Aniki.Services;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Aniki.ViewModels
{
    public class MainViewModel : ViewModelBase
    {

        private ObservableCollection<AnimeData> _animeList;
        private ICommand _refreshCommand;
        private ICommand _logoutCommand;

        private string _username;
        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        private Bitmap _profileImage;
        public Bitmap ProfileImage
        {
            get => _profileImage;
            set => SetProperty(ref _profileImage, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

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
            set => SetProperty(ref _selectedAnime, value);
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

        public MainViewModel() 
        {
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
                AnimeList.Clear();

                var animeListData = await MalUtils.LoadAnimeList(filter);

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
