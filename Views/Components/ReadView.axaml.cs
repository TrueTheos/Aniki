using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Aniki.Views;

internal sealed partial class ReadView : UserControl
{
    public ReadView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}