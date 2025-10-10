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

    private bool _mouseOverRoot = false;

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
        Console.WriteLine("MainButton entered");
        var scaleValue = "scale(1)";
        StatusButton1.RenderTransform = TransformOperations.Parse(scaleValue);
        StatusButton2.RenderTransform = TransformOperations.Parse(scaleValue);
        StatusButton3.RenderTransform = TransformOperations.Parse(scaleValue);
    }

    private void HideStatusButtons()
    {
        var hideScale = "scale(0)";
        if (!_mouseOverRoot)
        {
            StatusButton1.RenderTransform = TransformOperations.Parse(hideScale);
            StatusButton2.RenderTransform = TransformOperations.Parse(hideScale);
            StatusButton3.RenderTransform = TransformOperations.Parse(hideScale);
        }
    }

    private void RootPointerEnter()
    {
        Console.WriteLine("ROOT entered");
        _mouseOverRoot = true;
    }
    
    private void RootPointerExited()
    {
        Console.WriteLine("ROOT exited");
        _mouseOverRoot = false;
        HideStatusButtons();
    }
}