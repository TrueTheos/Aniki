using Aniki.Misc;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;

namespace Aniki.ViewModels;

public partial class AnimeCardViewModel : ViewModelBase
{
    [ObservableProperty]
    private int _animeId;
    
    [ObservableProperty]
    private string? _title;
    
    [ObservableProperty]
    private Bitmap? _image;
    
    [ObservableProperty]
    private AnimeStatusApi? _status;
    
    public void OnCardClicked()
    {
        MainViewModel vm = App.ServiceProvider.GetRequiredService<MainViewModel>();
        vm.GoToAnime(AnimeId);
    }
}