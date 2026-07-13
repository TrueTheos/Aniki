using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Aniki.Views;

internal sealed partial class UserAnimeListView : UserControl
{
    public UserAnimeListView()
    {
        InitializeComponent();
    }
    
    private async void GoToAnime(object? sender, RoutedEventArgs _)
    {
        try
        {
            if (sender is Button { DataContext: AnimeDetails anime })
            {
                await DependencyInjection.Instance.ServiceProvider!.GetRequiredService<MainViewModel>().GoToAnime(anime.Id).ConfigureAwait(true);
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
    }
}