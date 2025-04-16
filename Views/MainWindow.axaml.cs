using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System;

namespace Aniki
{
    public partial class MainWindow : Window
    {
        private string _accessToken;
        private TextBlock _userNameText;
        private Image _userProfileImage;
        private ComboBox _listFilterComboBox;
        private ListBox _animeListBox;
        private ObservableCollection<AnimeData> _animeList;

        private int _userId;

        public MainWindow() { InitializeComponent(); }

        public MainWindow(string accessToken)
        {
            _accessToken = accessToken;
            InitializeComponent();

            _userNameText = this.FindControl<TextBlock>("UserNameText");
            _userProfileImage = this.FindControl<Image>("UserProfileImage");
            _listFilterComboBox = this.FindControl<ComboBox>("ListFilterComboBox");
            _animeListBox = this.FindControl<ListBox>("AnimeListBox");
            _animeList = new ObservableCollection<AnimeData>();
            _animeListBox.ItemsSource = _animeList;
            _listFilterComboBox.SelectionChanged += ListFilterComboBox_SelectionChanged;

            _listFilterComboBox.ItemsSource = new List<string>
            {
                "All",
                "Currently Watching",
                "Completed",
                "On Hold",
                "Dropped",
                "Plan to Watch"
            };
            _listFilterComboBox.SelectedIndex = 0;

            _ = LoadUserDataAsync();
            _ = LoadAnimeListAsync();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async Task LoadUserDataAsync()
        {
            try
            {
                var userData = await MalUtils.LoadUserData();

                _userId = userData.Id;

                _userNameText.Text = userData.Name;

                await LoadProfileImageAsync();
            }
            catch (Exception ex)
            {
                _userNameText.Text = $"Error: {ex.Message}";
            }
        }

        private async Task LoadProfileImageAsync()
        {
            try
            {
                var profileImage = await ImageCache.GetUserProfileImage(_userId);
                if (profileImage != null)
                {
                    _userProfileImage.Source = profileImage;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading profile image: {ex.Message}");
            }
        }

        private async Task LoadAnimeListAsync(string status = null)
        {
            try
            {
                _animeList.Clear();

                var animeListData = await MalUtils.LoadAnimeList(status);

                foreach (var anime in animeListData)
                {
                    _animeList.Add(anime);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading anime list: {ex.Message}");
            }
        }

        private async void ListFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string selectedFilter = _listFilterComboBox.SelectedItem as string;
            await LoadAnimeListAsync(selectedFilter);
        }
    }
}