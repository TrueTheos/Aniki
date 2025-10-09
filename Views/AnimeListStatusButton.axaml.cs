using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
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

    private bool _isActionButtonsVisible;

    public AnimeListStatusButton()
    {
        InitializeComponent();
        
        PlusButton.PointerEntered += PlusButton_PointerEntered;
        PlusButton.PointerExited += PlusButton_PointerExited;
        ActionButtons.PointerEntered += ActionButtons_PointerEntered;
        ActionButtons.PointerExited += ActionButtons_PointerExited;
        
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

        if (status == null || status == AnimeStatusApi.none)
        {
            // Not on list - show Watching and Plan to Watch
            Button1Icon.Text = "▶";
            Button2Icon.Text = "📋";
            ToolTip.SetTip(Button1, "Add to Watching");
            ToolTip.SetTip(Button2, "Add to Plan to Watch");
        }
        else if (status == AnimeStatusApi.watching)
        {
            // Watching - show Completed and Plan to Watch
            Button1Icon.Text = "✓";
            Button2Icon.Text = "📋";
            ToolTip.SetTip(Button1, "Add to Completed");
            ToolTip.SetTip(Button2, "Add to Plan to Watch");
        }
        else if (status == AnimeStatusApi.completed)
        {
            // Completed - show Watching and Plan to Watch
            Button1Icon.Text = "▶";
            Button2Icon.Text = "📋";
            ToolTip.SetTip(Button1, "Add to Watching");
            ToolTip.SetTip(Button2, "Add to Plan to Watch");
        }
        else if (status == AnimeStatusApi.plan_to_watch)
        {
            // Plan to Watch - show Watching and Completed
            Button1Icon.Text = "▶";
            Button2Icon.Text = "✓";
            ToolTip.SetTip(Button1, "Add to Watching");
            ToolTip.SetTip(Button2, "Add to Completed");
        }
        else
        {
            // On Hold or Dropped - show Watching and Completed
            Button1Icon.Text = "▶";
            Button2Icon.Text = "✓";
            ToolTip.SetTip(Button1, "Add to Watching");
            ToolTip.SetTip(Button2, "Add to Completed");
        }
    }

    private void PlusButton_PointerEntered(object? sender, PointerEventArgs e)
    {
        ShowActionButtons();
    }

    private void PlusButton_PointerExited(object? sender, PointerEventArgs e)
    {
        if (!_isActionButtonsVisible)
        {
            HideActionButtons();
        }
    }

    private void ActionButtons_PointerEntered(object? sender, PointerEventArgs e)
    {
        _isActionButtonsVisible = true;
    }

    private void ActionButtons_PointerExited(object? sender, PointerEventArgs e)
    {
        _isActionButtonsVisible = false;
        HideActionButtons();
    }

    private void ShowActionButtons()
    {
        ActionButtons.IsVisible = true;
        ActionButtons.Opacity = 1;
    }

    private void HideActionButtons()
    {
        ActionButtons.Opacity = 0;
        ActionButtons.IsVisible = false;
    }

    private void Button1_Click(object? sender, PointerPressedEventArgs e)
    {
        var status = CurrentStatus;
        
        if (status == null || status == AnimeStatusApi.none)
        {
            StatusSelected?.Invoke(this, AnimeStatusApi.watching);
        }
        else if (status == AnimeStatusApi.watching)
        {
            StatusSelected?.Invoke(this, AnimeStatusApi.completed);
        }
        else
        {
            StatusSelected?.Invoke(this, AnimeStatusApi.watching);
        }
    }

    private void Button2_Click(object? sender, PointerPressedEventArgs e)
    {
        var status = CurrentStatus;
        
        if (status == null || status == AnimeStatusApi.none)
        {
            StatusSelected?.Invoke(this, AnimeStatusApi.plan_to_watch);
        }
        else if (status == AnimeStatusApi.watching)
        {
            StatusSelected?.Invoke(this, AnimeStatusApi.plan_to_watch);
        }
        else if (status == AnimeStatusApi.completed)
        {
            StatusSelected?.Invoke(this, AnimeStatusApi.plan_to_watch);
        }
        else if (status == AnimeStatusApi.plan_to_watch)
        {
            StatusSelected?.Invoke(this, AnimeStatusApi.completed);
        }
        else
        {
            StatusSelected?.Invoke(this, AnimeStatusApi.completed);
        }
    }

    public void ShowButton()
    {
        PlusButton.Opacity = 1;
    }

    public void HideButton()
    {
        PlusButton.Opacity = 0;
        HideActionButtons();
    }
}