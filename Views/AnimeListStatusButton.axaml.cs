using Aniki.Services.Anime;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;

namespace Aniki.Views;

public partial class AnimeListStatusButton : UserControl
{
    private bool _mouseOverRoot;
    private AnimeCardData? _data;

    private readonly IAnimeService _animeService;

    public AnimeListStatusButton()
    {
        InitializeComponent();
        
        _animeService = DependencyInjection.Instance.ServiceProvider!.GetRequiredService<IAnimeService>();
        
        MainButton.PointerEntered += (_, _) => ShowStatusButtons();
        MainButton.PointerExited += (_, _) => HideStatusButtons();
        Root.PointerExited += (_, _) => RootPointerExited();
        Root.PointerEntered += (_, _) => RootPointerEnter();
        
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AnimeCardData data)
        {
            _data = data;
        }
    }
    private void ShowStatusButtons()
    {
        PlannedToWatchButton.Classes.Add("visible");
        WatchingButton.Classes.Add("visible");
        CompletedButton.Classes.Add("visible");
        RemoveButton.Classes.Add("visible");

        if (_data!.MyListStatus != null)
        {
            switch (_data.MyListStatus.Value)
            {
                case AnimeStatus.None:
                    RemoveButton.IsVisible = false;
                    PlannedToWatchButton.IsVisible = true;
                    WatchingButton.IsVisible = true;
                    CompletedButton.IsVisible = true;
                    break;
                case AnimeStatus.Dropped:   
                    RemoveButton.IsVisible = true;
                    PlannedToWatchButton.IsVisible = true;
                    WatchingButton.IsVisible = true;
                    CompletedButton.IsVisible = true;
                    break;
                case AnimeStatus.OnHold:
                    RemoveButton.IsVisible = true;
                    PlannedToWatchButton.IsVisible = true;
                    WatchingButton.IsVisible = true;
                    CompletedButton.IsVisible = true;
                    break;
                case AnimeStatus.Completed:
                    RemoveButton.IsVisible = true;
                    PlannedToWatchButton.IsVisible = true;
                    WatchingButton.IsVisible = true;
                    CompletedButton.IsVisible = false;
                    break;
                case AnimeStatus.PlanToWatch:
                    RemoveButton.IsVisible = true;
                    PlannedToWatchButton.IsVisible = false;
                    WatchingButton.IsVisible = true;
                    CompletedButton.IsVisible = true;
                    break;
                case AnimeStatus.Watching:
                    RemoveButton.IsVisible = true;
                    PlannedToWatchButton.IsVisible = true;
                    WatchingButton.IsVisible = false;
                    CompletedButton.IsVisible = true;
                    break;
            }
        }
        else
        {
            RemoveButton.IsVisible = false;
            PlannedToWatchButton.IsVisible = true;
            WatchingButton.IsVisible = true;
            CompletedButton.IsVisible = true;
        }
    }

    private void HideStatusButtons()
    {
        if (!_mouseOverRoot)
        {
            PlannedToWatchButton.Classes.Remove("visible");
            WatchingButton.Classes.Remove("visible");
            CompletedButton.Classes.Remove("visible");
            RemoveButton.Classes.Remove("visible");
        }
    }

    private void RootPointerEnter()
    {
        _mouseOverRoot = true;
    }
    
    private void RootPointerExited()
    {
        _mouseOverRoot = false;
        HideStatusButtons();
    }
    
    private void OnStatusButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string statusStr)
        {
            if (Enum.TryParse<AnimeStatusApi>(statusStr, out AnimeStatusApi status))
            {
                if (status == AnimeStatusApi.none)
                {
                    _animeService.RemoveFromUserListAsync(_data!.AnimeId);
                }
                else
                {
                    _animeService.SetAnimeStatusAsync(_data!.AnimeId, StatusEnum.ToAnimeStatus(status.ToString()).TranslatedToAnimeStatus());
                }

                _data!.MyListStatus = StatusEnum.ToAnimeStatus(status.ToString()).TranslatedToAnimeStatus();
                HideStatusButtons();
                ShowStatusButtons();
            }
        }
    }
}