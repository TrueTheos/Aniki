using Aniki.Misc;
using Aniki.Services.Interfaces;
using Avalonia.Media.Imaging;
using Avalonia.Svg.Skia;
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
    public string TranslatedStatus => Status != null ? StatusEnum.ApiToTranslated(Status.Value).ToString() : "None";
    
    private readonly MainViewModel _mainVm;
    private readonly IMalService _malService;
    
    public AnimeCardViewModel()
    {
        _mainVm = App.ServiceProvider.GetRequiredService<MainViewModel>();
        _malService = App.ServiceProvider.GetRequiredService<IMalService>();
    }
    
    partial void OnStatusChanged(AnimeStatusApi? value)
    {
        OnPropertyChanged(nameof(TranslatedStatus));
    }
    
    public void OnCardClicked()
    {
        _mainVm.GoToAnime(AnimeId);
    }

    [RelayCommand]
    private void AddToWatching()
    {
        _malService.UpdateAnimeStatus(AnimeId, AnimeStatusApi.watching);
        Status = AnimeStatusApi.watching;
    }
    
    [RelayCommand]
    private void AddToCompleted()
    {
        _malService.UpdateAnimeStatus(AnimeId, AnimeStatusApi.completed);
        Status = AnimeStatusApi.completed;
    }
    
    [RelayCommand]
    private void AddToPlanned()
    {
        _malService.UpdateAnimeStatus(AnimeId, AnimeStatusApi.plan_to_watch);
        Status = AnimeStatusApi.plan_to_watch;
    }

    [RelayCommand]
    private void RemoveFromList()
    {
        _malService.RemoveFromList(AnimeId);
        Status = AnimeStatusApi.none;
    }
}