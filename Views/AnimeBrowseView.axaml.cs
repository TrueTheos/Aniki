using Avalonia.Controls;
using Aniki.Misc;

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
}