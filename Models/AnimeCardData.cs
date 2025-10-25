using Aniki.Misc;
using Avalonia.Media.Imaging;

namespace Aniki.Models;

public partial class AnimeCardData : ObservableObject
{
    [ObservableProperty]
    private int _animeId;
    
    [ObservableProperty]
    private string? _title;
    
    [ObservableProperty]
    private Bitmap? _image;
    
    [ObservableProperty]
    private AnimeStatusApi? _status;
}