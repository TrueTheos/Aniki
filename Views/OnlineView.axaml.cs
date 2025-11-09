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
            var videoView = this.FindControl<VideoView>("VideoView");
            var container = this.FindControl<Panel>("VideoContainer");
            
            if (videoView != null && container != null)
            {
                viewModel.RegisterVideoView(videoView, container);
            }
        }
    }
}