using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Aniki.Misc;
using System;
using Avalonia.Media.Transformation;

namespace Aniki.Views;

public partial class AnimeListStatusButton : UserControl
{
    public static readonly StyledProperty<AnimeStatusApi?> CurrentStatusProperty =
        AvaloniaProperty.Register<AnimeListStatusButton, AnimeStatusApi?>(nameof(CurrentStatus));

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
    }

    private void ShowStatusButtons()
    {
        // Scale status buttons up (they will animate from 0 to 0.8)
        var scaleValue = "scale(0.8)";
        StatusButton1.RenderTransform = TransformOperations.Parse(scaleValue);
        StatusButton2.RenderTransform = TransformOperations.Parse(scaleValue);
        StatusButton3.RenderTransform = TransformOperations.Parse(scaleValue);
    }

    private void HideStatusButtons()
    {
        // Scale status buttons back down to 0 (hidden)
        var hideScale = "scale(0)";
        StatusButton1.RenderTransform = TransformOperations.Parse(hideScale);
        StatusButton2.RenderTransform = TransformOperations.Parse(hideScale);
        StatusButton3.RenderTransform = TransformOperations.Parse(hideScale);
    }
}