using Avalonia.Controls;
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
}