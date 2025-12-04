using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;

namespace Aniki.Views;

public partial class AnimeBrowseView : UserControl
{
    public AnimeBrowseView()
    {
        InitializeComponent();
    }

    protected override async void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
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
    
    private const double CARD_WIDTH = 160;
    private const double CARD_SPACING = 20;
    private const double SCROLL_AMOUNT = CARD_WIDTH + CARD_SPACING;

    private void ScrollLeft(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.CommandParameter is ScrollViewer scroller)
        {
            double newOffset = scroller.Offset.X - SCROLL_AMOUNT;
            scroller.Offset = new Vector(newOffset, scroller.Offset.Y);
        }
    }

    private void ScrollRight(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.CommandParameter is ScrollViewer scroller)
        {
            double newOffset = scroller.Offset.X + SCROLL_AMOUNT;
            scroller.Offset = new Vector(newOffset, scroller.Offset.Y);
        }
    }
}