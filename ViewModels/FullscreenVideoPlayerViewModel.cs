using LibVLCSharp.Avalonia;
using LibVLCSharp.Shared;

namespace Aniki.ViewModels;

public partial class FullscreenVideoPlayerViewModel : ViewModelBase, IDisposable
{
    [ObservableProperty]
    private MediaPlayer? _mediaPlayer;

    public FullscreenVideoPlayerViewModel()
    {
    }

    public void OnInitialized(MediaPlayer mediaPlayer)
    {
        MediaPlayer = mediaPlayer;
    }
    
    [RelayCommand]
    private void Play()
    {
        MediaPlayer?.Play();
    }

    [RelayCommand]
    private void Pause()
    {
        MediaPlayer?.Pause();
    }

    [RelayCommand]
    private void Stop()
    {
        MediaPlayer?.Stop();
    }
    
    public void Dispose()
    {
    }
}