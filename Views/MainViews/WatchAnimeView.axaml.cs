using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Aniki.Views;

internal sealed partial class WatchAnimeView : UserControl
{
    public WatchAnimeView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
        => AvaloniaXamlLoader.Load(this);
}