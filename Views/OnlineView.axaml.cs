using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Aniki.Views;

public partial class OnlineView : UserControl
{
    public OnlineView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}