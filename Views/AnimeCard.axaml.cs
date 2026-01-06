using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;

namespace Aniki.Views;

public partial class AnimeCard : UserControl
{
    private AnimeCardData? _data;
    
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
            if (e.PropertyName == nameof(AnimeCardData.UserStatus))
                _data.UserStatus = data.UserStatus;
        }
    }

    
    private void OnTapped(object? sender, TappedEventArgs e)
    {
        if (_data is null) return;
        DependencyInjection.Instance.ServiceProvider!.GetRequiredService<MainViewModel>().GoToAnime(_data.AnimeId);
    }
}