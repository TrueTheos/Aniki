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
    
    private async void GoToAnime(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: AnimeDetails anime })
        {
            await DependencyInjection.Instance.ServiceProvider!.GetRequiredService<MainViewModel>().GoToAnime(anime.Id);
        }
    }
}