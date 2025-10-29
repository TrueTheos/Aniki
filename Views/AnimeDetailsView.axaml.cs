using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Aniki.Views;

public partial class AnimeDetailsView : UserControl
{
    public AnimeDetailsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
        => AvaloniaXamlLoader.Load(this);
}