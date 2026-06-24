using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Aniki.Views;

public partial class DownloadedView : UserControl
{
    private readonly DownloadedViewModel _viewModel;
    
    public DownloadedView()
    {
        InitializeComponent();
        
        _viewModel = DependencyInjection.Instance.ServiceProvider!.GetRequiredService<DownloadedViewModel>();
    }

    private void OnDoubleTapEpisode(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: DownloadedEpisode ep })
        {
            _viewModel.LaunchEpisode(ep);
        }
    }
    
    private async void OnDoubleTapAnime(object? sender, TappedEventArgs _)
    {
        try
        {
            if (sender is Control { DataContext: AnimeGroup anime })
            {
                await _viewModel.OpenAnimeDetailsCommand.ExecuteAsync(anime.MalId);
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
    }
    
    private void OnDoubleTapIgnore(object? sender, TappedEventArgs e)
    {
        e.Handled = true;
    }

    private void OnSearchIconClick(object? sender, RoutedEventArgs e)
    {
        SearchTextBox?.Focus();
    }
}
