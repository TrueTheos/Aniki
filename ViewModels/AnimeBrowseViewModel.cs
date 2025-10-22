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
            var airing = await _malService.GetTopAnimeInCategory(MalService.AnimeRankingCategory.AIRING);
            await LoadAnimeCards(airing, PopularThisSeason);

            var upcoming = await _malService.GetTopAnimeInCategory(MalService.AnimeRankingCategory.UPCOMING);
            await LoadAnimeCards(upcoming, PopularUpcoming);
            
            var allTime = await _malService.GetTopAnimeInCategory(MalService.AnimeRankingCategory.BYPOPULARITY);
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

    private async Task LoadAnimeCards(List<MAL_RankingEntry> animeList, ObservableCollection<AnimeCardViewModel> collection)
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
                    Status = details.MyListStatus?.Status
                };
                _ = LoadAnimeImageAsync(card, details.MainPicture);
                collection.Add(card);
            }
        }
    }
    
    [RelayCommand]
    private async Task PerformSearchAsync()
    {
        try
        {
            IsLoading = true;
            IsSearchMode = true;
            ShowCategories = false;

            _allSearchResults = await _malService.SearchAnimeOrdered(SearchQuery);
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

    [RelayCommand]
    private void GoBack()
    {
        SearchQuery = string.Empty;
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