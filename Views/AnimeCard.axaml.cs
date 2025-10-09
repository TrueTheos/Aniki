using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Aniki.Misc;
using Aniki.Models;
using System;

namespace Aniki.Views;

public partial class AnimeCard : UserControl
{
    public static readonly StyledProperty<Bitmap?> ImageProperty =
        AvaloniaProperty.Register<AnimeCard, Bitmap?>(nameof(Image));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AnimeCard, string?>(nameof(Title));

    public static readonly StyledProperty<AnimeStatusApi?> StatusProperty =
        AvaloniaProperty.Register<AnimeCard, AnimeStatusApi?>(nameof(Status));

    public static readonly StyledProperty<int> AnimeIdProperty =
        AvaloniaProperty.Register<AnimeCard, int>(nameof(AnimeId));

    public Bitmap? Image
    {
        get => GetValue(ImageProperty);
        set => SetValue(ImageProperty, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public AnimeStatusApi? Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public int AnimeId
    {
        get => GetValue(AnimeIdProperty);
        set => SetValue(AnimeIdProperty, value);
    }

    public event EventHandler<(int AnimeId, AnimeStatusApi Status)>? StatusChangeRequested;
    public event EventHandler<int>? AnimeClicked;

    public AnimeCard()
    {
        InitializeComponent();
        
        PropertyChanged += OnPropertyChanged;
        StatusButton.StatusSelected += OnStatusSelected;
        PointerPressed += OnCardClicked;
    }

    private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ImageProperty)
        {
            AnimeImage.Source = Image;
        }
        else if (e.Property == TitleProperty)
        {
            AnimeTitle.Text = Title;
        }
        else if (e.Property == StatusProperty)
        {
            StatusButton.CurrentStatus = Status;
        }
    }

    private void OnStatusSelected(object? sender, AnimeStatusApi status)
    {
        StatusChangeRequested?.Invoke(this, (AnimeId, status));
    }

    private void OnCardClicked(object? sender, PointerPressedEventArgs e)
    {
        AnimeClicked?.Invoke(this, AnimeId);
    }
}