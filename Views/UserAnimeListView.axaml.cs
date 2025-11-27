using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;

namespace Aniki.Views;

public partial class UserAnimeListView : UserControl
{
    public UserAnimeListView()
    {
        InitializeComponent();
    }
    
    private void GoToAnime(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is MalAnimeDetails anime)
        {
            App.ServiceProvider.GetRequiredService<MainViewModel>().GoToAnime(anime!.Id);
        }
    }

}