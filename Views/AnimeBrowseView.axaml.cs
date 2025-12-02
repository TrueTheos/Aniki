using Aniki.Misc;
using Aniki.ViewModels;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;

namespace Aniki.Views;

public partial class AnimeBrowseView : UserControl
{
    public AnimeBrowseView()
    {
        InitializeComponent();
    }

    protected override async void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        
        if (DataContext is AnimeBrowseViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }
    
    private void OnDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (sender is Border { DataContext: AnimeCardData anime })
        {
            DependencyInjection.Instance.ServiceProvider!.GetRequiredService<MainViewModel>().GoToAnime(anime.AnimeId);
        }
    }
    
    private const double CardWidth = 160;
    private const double CardSpacing = 20;
    private const double ScrollAmount = CardWidth + CardSpacing;

    private void ScrollLeft(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.CommandParameter is ScrollViewer scroller)
        {
            var newOffset = scroller.Offset.X - ScrollAmount;
            scroller.Offset = new Vector(newOffset, scroller.Offset.Y);
        }
    }

    private void ScrollRight(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.CommandParameter is ScrollViewer scroller)
        {
            var newOffset = scroller.Offset.X + ScrollAmount;
            scroller.Offset = new Vector(newOffset, scroller.Offset.Y);
        }
    }
}