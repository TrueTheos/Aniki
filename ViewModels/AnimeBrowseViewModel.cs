using Aniki.Misc;
using Aniki.Models;
using Aniki.Services.Interfaces;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Aniki.ViewModels;

public partial class AnimeBrowseViewModel : ViewModelBase
{
    private readonly IMalService _malService;
    
    [ObservableProperty]
    private ObservableCollection<AnimeCardViewModel> _trendingNow = new();
    
    [ObservableProperty]
    private ObservableCollection<AnimeCardViewModel> _popularThisSeason = new();
    
    [ObservableProperty]
    private ObservableCollection<AnimeCardViewModel> _popularUpcoming = new();
    
    [ObservableProperty]
    private ObservableCollection<AnimeCardViewModel> _trendingAllTime = new();
    
    [ObservableProperty]
    private ObservableCollection<AnimeCardViewModel> _searchResults = new();
    
    [ObservableProperty]
    private string _searchQuery = string.Empty;
    
    [ObservableProperty]
    private bool _isSearchMode;
    
    [ObservableProperty]
    private bool _isLoading;
    
    [ObservableProperty]
    private bool _showCategories = true;
    
    [ObservableProperty]
    private int _currentPage = 1;
    
    [ObservableProperty]
    private int _totalPages = 1;
    
    [ObservableProperty]
    private bool _canGoNext;
    
    [ObservableProperty]
    private bool _canGoPrevious;

    private List<MAL_SearchEntry> _allSearchResults = new();
    private const int PageSize = 10;
    private CancellationTokenSource? _searchCts;

    public AnimeBrowseViewModel(IMalService malService)
    {
        _malService = malService;
    }

    public async Task InitializeAsync()
    {
        await LoadCategoriesAsync();
    }

    private async Task LoadCategoriesAsync()
    {
        IsLoading = true;
        try
        {
            // For demo purposes, we'll use the user's anime list to populate categories
            // In a real app, you'd have dedicated API endpoints for these categories
            var userList = await _malService.GetUserAnimeList();
            
            // Trending Now - Recent watching
            var watching = userList
                .Where(a => a.ListStatus?.Status == AnimeStatusApi.watching)
                .Take(10)
                .ToList();
            await LoadAnimeCards(watching, TrendingNow);
            
            // Popular This Season - Currently airing
            var airing = userList
                .Where(a => a.Node.Status == "currently_airing")
                .Take(10)
                .ToList();
            await LoadAnimeCards(airing, PopularThisSeason);
            
            // Popular Upcoming - Plan to watch
            var upcoming = userList
                .Where(a => a.ListStatus?.Status == AnimeStatusApi.plan_to_watch)
                .Take(10)
                .ToList();
            await LoadAnimeCards(upcoming, PopularUpcoming);
            
            // Trending All Time - Completed with high scores
            var allTime = userList
                .Where(a => a.ListStatus?.Status == AnimeStatusApi.completed && a.ListStatus.Score >= 8)
                .Take(10)
                .ToList();
            await LoadAnimeCards(allTime, TrendingAllTime);
        }
        catch (Exception ex)
        {
            Log.Information($"Error loading categories: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadAnimeCards(List<MAL_AnimeData> animeList, ObservableCollection<AnimeCardViewModel> collection)
    {
        collection.Clear();
        foreach (var anime in animeList)
        {
            var details = await _malService.GetAnimeDetails(anime.Node.Id);
            if (details != null)
            {
                var card = new AnimeCardViewModel
                {
                    AnimeId = details.Id,
                    Title = details.Title,
                    Image = details.Picture,
                    Status = details.MyListStatus?.Status
                };
                collection.Add(card);
            }
        }
    }

    partial void OnSearchQueryChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            ExitSearchMode();
        }
        else
        {
            _ = PerformSearchAsync(value);
        }
    }

    private async Task PerformSearchAsync(string query)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        try
        {
            await Task.Delay(300, token); // Debounce
            
            if (token.IsCancellationRequested) return;

            IsLoading = true;
            IsSearchMode = true;
            ShowCategories = false;

            _allSearchResults = await _malService.SearchAnimeOrdered(query);
            CurrentPage = 1;
            TotalPages = (int)Math.Ceiling(_allSearchResults.Count / (double)PageSize);
            
            LoadSearchResultsPage();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Log.Information($"Error searching: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void LoadSearchResultsPage()
    {
        SearchResults.Clear();
        
        var pageResults = _allSearchResults
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize);

        foreach (var result in pageResults)
        {
            var card = new AnimeCardViewModel
            {
                AnimeId = result.MalAnime.Id,
                Title = result.MalAnime.Title,
                Status = null // Will be loaded if needed
            };
            
            // Load image async
            _ = LoadAnimeImageAsync(card, result.MalAnime.MainPicture);
            
            SearchResults.Add(card);
        }

        UpdatePaginationState();
    }

    private async Task LoadAnimeImageAsync(AnimeCardViewModel card, MAL_MainPicture? picture)
    {
        if (picture != null)
        {
            card.Image = await _malService.GetAnimeImage(picture);
        }
    }

    private void UpdatePaginationState()
    {
        CanGoPrevious = CurrentPage > 1;
        CanGoNext = CurrentPage < TotalPages;
    }

    [RelayCommand]
    private void NextPage()
    {
        if (CanGoNext)
        {
            CurrentPage++;
            LoadSearchResultsPage();
        }
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (CanGoPrevious)
        {
            CurrentPage--;
            LoadSearchResultsPage();
        }
    }

    private void ExitSearchMode()
    {
        IsSearchMode = false;
        ShowCategories = true;
        SearchResults.Clear();
        _allSearchResults.Clear();
    }

    public async Task UpdateAnimeStatusAsync(int animeId, AnimeStatusApi newStatus)
    {
        try
        {
            await _malService.UpdateAnimeStatus(animeId, MalService.AnimeStatusField.STATUS, StatusEnum.ApiToString(newStatus));
            
            // Update the card status in all collections
            UpdateCardStatus(TrendingNow, animeId, newStatus);
            UpdateCardStatus(PopularThisSeason, animeId, newStatus);
            UpdateCardStatus(PopularUpcoming, animeId, newStatus);
            UpdateCardStatus(TrendingAllTime, animeId, newStatus);
            UpdateCardStatus(SearchResults, animeId, newStatus);
        }
        catch (Exception ex)
        {
            Log.Information($"Error updating anime status: {ex.Message}");
        }
    }

    private void UpdateCardStatus(ObservableCollection<AnimeCardViewModel> collection, int animeId, AnimeStatusApi status)
    {
        var card = collection.FirstOrDefault(c => c.AnimeId == animeId);
        if (card != null)
        {
            card.Status = status;
        }
    }
}

public partial class AnimeCardViewModel : ObservableObject
{
    [ObservableProperty]
    private int _animeId;
    
    [ObservableProperty]
    private string? _title;
    
    [ObservableProperty]
    private Bitmap? _image;
    
    [ObservableProperty]
    private AnimeStatusApi? _status;
}