using Avalonia;
using Avalonia.Controls;
using Aniki.Misc;
using Aniki.Services.Interfaces;
using Avalonia.Interactivity;
using Avalonia.Media.Transformation;
using Microsoft.Extensions.DependencyInjection;

namespace Aniki.Views;

public partial class AnimeListStatusButton : UserControl
{
    private bool _mouseOverRoot;
    private AnimeCardData? _data = null;

    private IMalService _malService;

    public AnimeListStatusButton()
    {
        InitializeComponent();
        
        _malService = App.ServiceProvider.GetRequiredService<IMalService>();
        
        MainButton.PointerEntered += (_, __) => ShowStatusButtons();
        MainButton.PointerExited += (_, __) => HideStatusButtons();
        Root.PointerExited += (_, __) => RootPointerExited();
        Root.PointerEntered += (_, __) => RootPointerEnter();
        
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
                case AnimeStatusApi.none:
                    RemoveButton.IsVisible = false;
                    PlannedToWatchButton.IsVisible = true;
                    WatchingButton.IsVisible = true;
                    CompletedButton.IsVisible = true;
                    break;
                case AnimeStatusApi.dropped:   
                    RemoveButton.IsVisible = true;
                    PlannedToWatchButton.IsVisible = true;
                    WatchingButton.IsVisible = true;
                    CompletedButton.IsVisible = true;
                    break;
                case AnimeStatusApi.on_hold:
                    RemoveButton.IsVisible = true;
                    PlannedToWatchButton.IsVisible = true;
                    WatchingButton.IsVisible = true;
                    CompletedButton.IsVisible = true;
                    break;
                case AnimeStatusApi.completed:
                    RemoveButton.IsVisible = true;
                    PlannedToWatchButton.IsVisible = true;
                    WatchingButton.IsVisible = true;
                    CompletedButton.IsVisible = false;
                    break;
                case AnimeStatusApi.plan_to_watch:
                    RemoveButton.IsVisible = true;
                    PlannedToWatchButton.IsVisible = false;
                    WatchingButton.IsVisible = true;
                    CompletedButton.IsVisible = true;
                    break;
                case AnimeStatusApi.watching:
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
            if (Enum.TryParse<AnimeStatusApi>(statusStr, out var status))
            {
                if (status == AnimeStatusApi.none)
                {
                    _malService.RemoveFromList(_data!.AnimeId);
                }
                else
                {
                    _malService.UpdateAnimeStatus(_data!.AnimeId, status);
                }

                _data!.MyListStatus = status;
                HideStatusButtons();
                ShowStatusButtons();
            }
        }
    }
}