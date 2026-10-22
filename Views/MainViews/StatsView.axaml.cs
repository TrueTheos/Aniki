using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace Aniki.Views;

internal sealed partial class StatsView : UserControl
{
    public StatsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
        => AvaloniaXamlLoader.Load(this);

    private async void OnAnimeTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            if (sender is Control { DataContext: AnimeDetails anime })
                await DependencyInjection.Instance.ServiceProvider!.GetRequiredService<MainViewModel>().GoToMedia(anime.Id).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }
}
