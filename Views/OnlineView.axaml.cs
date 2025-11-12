using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Aniki.ViewModels;
using LibVLCSharp.Avalonia;

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
          this.DataContextChanged += OnDataContextChanged;
        
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is OnlineViewModel viewModel)
        {
            var videoContainer = this.FindControl<Panel>("VideoContainer");
            var originalParent = this.FindControl<Border>("VideoHost"); 
                
            if (videoContainer != null && originalParent != null)
            {
                viewModel.RegisterVideoPlayer(videoContainer, originalParent);
            }
        }
    }
}