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

        if (CurrentStatus != null)
        {
            switch (CurrentStatus.Value)
            {
                case AnimeStatusApi.none:
                case AnimeStatusApi.dropped:   
                case AnimeStatusApi.on_hold:
                    PlannedToWatchButton.IsVisible = true;
                    WatchingButton.IsVisible = true;
                    CompletedButton.IsVisible = true;
                    break;
                case AnimeStatusApi.completed:
                    PlannedToWatchButton.IsVisible = true;
                    WatchingButton.IsVisible = true;
                    CompletedButton.IsVisible = false;
                    break;
                case AnimeStatusApi.plan_to_watch:
                    PlannedToWatchButton.IsVisible = false;
                    WatchingButton.IsVisible = true;
                    CompletedButton.IsVisible = true;
                    break;
                case AnimeStatusApi.watching:
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