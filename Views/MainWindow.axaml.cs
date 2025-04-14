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

            // Populate filter options
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

            // Load user data and anime list
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
                // Use MalUtils to load user data
                var userData = await MalUtils.LoadUserData(_accessToken);

                // Set username
                _userNameText.Text = userData.Name;
            }
            catch (Exception ex)
            {
                // Handle error
                _userNameText.Text = $"Error: {ex.Message}";
            }
        }

        private async Task LoadAnimeListAsync(string status = null)
        {
            try
            {
                _animeList.Clear();

                // Use MalUtils to load anime list
                var animeListData = await MalUtils.LoadAnimeList(_accessToken, status);

                // Update the observable collection
                foreach (var anime in animeListData)
                {
                    _animeList.Add(anime);
                }
            }
            catch (Exception ex)
            {
                // Handle error - you could show a message to the user
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