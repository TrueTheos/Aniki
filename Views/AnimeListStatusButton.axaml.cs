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
        var scaleValue = "scale(1)";
        PlannedToWatchButton.RenderTransform = TransformOperations.Parse(scaleValue);
        WatchingButton.RenderTransform = TransformOperations.Parse(scaleValue);
        CompletedButton.RenderTransform = TransformOperations.Parse(scaleValue);
    }

    private void HideStatusButtons()
    {
        var hideScale = "scale(0)";
        if (!_mouseOverRoot)
        {
            WatchingButton.RenderTransform = TransformOperations.Parse(hideScale);
            PlannedToWatchButton.RenderTransform = TransformOperations.Parse(hideScale);
            CompletedButton.RenderTransform = TransformOperations.Parse(hideScale);
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