using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Aniki.Views;

public partial class AnimeBrowseView : UserControl
{
    private readonly AnimeBrowseViewModel _viewModel;
    
    public AnimeBrowseView()
    {
        _viewModel = DependencyInjection.Instance.ServiceProvider!.GetRequiredService<AnimeBrowseViewModel>();
        InitializeComponent();
    }

    protected override async void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        try
        {
            base.OnAttachedToVisualTree(e);
        
            await _viewModel.InitializeAsync();
        }
        catch (Exception error)
        {
            Debug.WriteLine(error);
        }
    }
    
    private async void OnDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (sender is Border { DataContext: AnimeCardData anime })
        {
            await DependencyInjection.Instance.ServiceProvider!.GetRequiredService<MainViewModel>().GoToAnime(anime.AnimeId);
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