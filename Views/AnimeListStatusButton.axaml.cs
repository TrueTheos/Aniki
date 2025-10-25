using Avalonia;
using Avalonia.Controls;
using Aniki.Misc;
using Avalonia.Media.Transformation;

namespace Aniki.Views;

public partial class AnimeListStatusButton : UserControl
{
    public static readonly StyledProperty<AnimeStatusApi?> CurrentStatusProperty =
        AvaloniaProperty.Register<AnimeListStatusButton, AnimeStatusApi?>(nameof(CurrentStatus));

    private bool _mouseOverRoot;

    public AnimeStatusApi? CurrentStatus
    {
        get => GetValue(CurrentStatusProperty);
        set => SetValue(CurrentStatusProperty, value);
    }

    public AnimeListStatusButton()
    {
        InitializeComponent();
        MainButton.PointerEntered += (_, __) => ShowStatusButtons();
        MainButton.PointerExited += (_, __) => HideStatusButtons();
        Root.PointerExited += (_, __) => RootPointerExited();
        Root.PointerEntered += (_, __) => RootPointerEnter();
    }

    private void ShowStatusButtons()
    {
        PlannedToWatchButton.Classes.Add("visible");
        WatchingButton.Classes.Add("visible");
        CompletedButton.Classes.Add("visible");
        RemoveButton.Classes.Add("visible");

        if (CurrentStatus != null)
        {
            switch (CurrentStatus.Value)
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
                    RemoveButton.IsCancel = true;
                    PlannedToWatchButton.IsVisible = false;
                    WatchingButton.IsVisible = true;
                    CompletedButton.IsVisible = true;
                    break;
                case AnimeStatusApi.watching:
                    RemoveButton.IsCancel = true;
                    PlannedToWatchButton.IsVisible = true;
                    WatchingButton.IsVisible = false;
                    CompletedButton.IsVisible = true;
                    break;
            }
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
}