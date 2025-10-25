using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Aniki.Misc;
using Avalonia.Interactivity;

namespace Aniki.Views;

public partial class AnimeCard : UserControl
{
    private AnimeCardViewModel? _viewModel;

    public AnimeCard()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AnimeCardData data)
        {
            _viewModel = new AnimeCardViewModel
            {
                AnimeId = data.AnimeId,
                Title = data.Title,
                Image = data.Image,
                Status = data.Status
            };
            
            data.PropertyChanged += OnDataPropertyChanged;
            
            StatusButton.DataContext = _viewModel;
        }
    }

    private void OnDataPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel != null && sender is AnimeCardData data)
        {
            if (e.PropertyName == nameof(AnimeCardData.Status))
                _viewModel.Status = data.Status;
        }
    }

    
    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is AnimeCardViewModel viewModel)
        {
            viewModel.OnCardClicked();
        }
    }
}