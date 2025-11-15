using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using LibVLCSharp.Shared;

namespace Aniki.Views;

public partial class FullscreenVideoPlayer : Window
{
    private MediaPlayer _mediaPlayer;
    private FullscreenVideoPlayerViewModel _vm;
    
    public FullscreenVideoPlayer(FullscreenVideoPlayerViewModel viewModel, MediaPlayer mediaplayer)
    {
        InitializeComponent();
        #if DEBUG
            this.AttachDevTools();
        #endif

        DataContext = viewModel;

        _vm = viewModel;
        _mediaPlayer = mediaplayer;

        Loaded += LoadMediaPlayer;
    }

    private void LoadMediaPlayer(object? sender, RoutedEventArgs routedEventArgs)
    {
        _vm.OnInitialized(_mediaPlayer);
        WindowState = WindowState.FullScreen;
    }

    public void OnClose(object? sender, RoutedEventArgs routedEventArgs)
    {
        Close();
    }
}