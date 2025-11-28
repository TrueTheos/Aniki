namespace Aniki.Models;

public partial class AnimeCardData : ObservableObject
{
    [ObservableProperty]
    private int _animeId;
    
    [ObservableProperty]
    private string? _title;

    [ObservableProperty] 
    private string? _imageUrl;

    [ObservableProperty] private float _score;
    
    [ObservableProperty]
    private AnimeStatusApi? _myListStatus;
}