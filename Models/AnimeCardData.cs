namespace Aniki.Models;

internal sealed partial class AnimeCardData : ObservableObject
{
    [ObservableProperty]
    private int _animeId;
    
    [ObservableProperty]
    private string? _title;

    [ObservableProperty] 
    private string? _imageUrl;

    [ObservableProperty] private float _score;
    
    [ObservableProperty]
    private AnimeStatus? _userStatus;
}