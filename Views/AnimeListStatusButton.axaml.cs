using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Aniki.Misc;
using System;

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

    public event EventHandler<AnimeStatusApi>? StatusSelected;

    public AnimeListStatusButton()
    {
        InitializeComponent();
        PropertyChanged += OnPropertyChanged;
    }

    private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == CurrentStatusProperty)
        {
            UpdateButtons();
        }
    }

    private void UpdateButtons()
    {
        var status = CurrentStatus;

        ButtonWatching.IsVisible = status != AnimeStatusApi.watching;
        ButtonCompleted.IsVisible = status != AnimeStatusApi.completed;
        ButtonPlanToWatch.IsVisible = status != AnimeStatusApi.plan_to_watch;
    }

    private void ButtonWatching_Click(object? sender, PointerPressedEventArgs e)
    {
        StatusSelected?.Invoke(this, AnimeStatusApi.watching);
    }

    private void ButtonCompleted_Click(object? sender, PointerPressedEventArgs e)
    {
        StatusSelected?.Invoke(this, AnimeStatusApi.completed);
    }

    private void ButtonPlanToWatch_Click(object? sender, PointerPressedEventArgs e)
    {
        StatusSelected?.Invoke(this, AnimeStatusApi.plan_to_watch);
    }
}