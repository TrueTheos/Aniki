using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;

namespace Aniki.Views;

public partial class DownloadedView : UserControl
{
    public DownloadedView()
    {
        InitializeComponent();
    }

    private void OnDoubleTapEpisode(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: DownloadedEpisode ep })
        {
            DependencyInjection.Instance.ServiceProvider!.GetRequiredService<DownloadedViewModel>().LaunchEpisode(ep);
        }
    }

    private void OnSearchIconClick(object? sender, RoutedEventArgs e)
    {
        SearchTextBox?.Focus();
    }
}
