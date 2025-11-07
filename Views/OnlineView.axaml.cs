using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Aniki.ViewModels;

namespace Aniki.Views;

public partial class OnlineView : UserControl
{
    public OnlineView()
    {
        InitializeComponent();
        
        // Attach event handlers after loading
        this.AttachedToVisualTree += OnAttachedToVisualTree;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
    }
}