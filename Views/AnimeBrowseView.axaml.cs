using Avalonia.Controls;
using Aniki.Misc;
using Avalonia.Input;
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
        if (sender is Border border && border.DataContext is AnimeCardData anime)
        {
            App.ServiceProvider.GetRequiredService<MainViewModel>().GoToAnime(anime!.AnimeId);
        }
    }
}