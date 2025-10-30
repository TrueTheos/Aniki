using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Aniki.Misc;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;

namespace Aniki.Views;

public partial class AnimeCard : UserControl
{
    private AnimeCardData? _data = null;
    
    public AnimeCard()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AnimeCardData data)
        {
            _data = data;
            _data.PropertyChanged += OnDataPropertyChanged;
        }
    }

    private void OnDataPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_data != null && sender is AnimeCardData data)
        {
            if (e.PropertyName == nameof(AnimeCardData.Status))
                _data.Status = data.Status;
        }
    }

    
    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        App.ServiceProvider.GetRequiredService<MainViewModel>().GoToAnime(_data!.AnimeId);
    }
}