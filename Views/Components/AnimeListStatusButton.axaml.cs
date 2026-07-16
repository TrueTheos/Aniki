using System.ComponentModel;
using Aniki.Services;
using Aniki.Services.Anime;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Svg.Skia;

namespace Aniki.Views;

internal sealed partial class AnimeListStatusButton : UserControl
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
        
        DataContextChanged += (_, _) => BindDataContext();
        Loaded += (_, _) => BindDataContext();
    }

    private void BindDataContext()
    {
        if (_data != null)
            _data.PropertyChanged -= OnDataPropertyChanged;

        _data = DataContext as AnimeCardData;

        if (_data != null)
            _data.PropertyChanged += OnDataPropertyChanged;

        UpdateMainButtonIcon();
    }

    private void OnDataPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AnimeCardData.UserStatus))
            UpdateMainButtonIcon();
    }
    private void ShowStatusButtons()
    {
        PlannedToWatchButton.Classes.Add("visible");
        WatchingButton.Classes.Add("visible");
        CompletedButton.Classes.Add("visible");
        RemoveButton.Classes.Add("visible");

        if (_data!.UserStatus != null)
        {
            switch (_data.UserStatus.Value)
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
    
    private async void OnStatusButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string statusStr }) return;
        if (!Enum.TryParse(statusStr, out AnimeStatusApi status)) return;
        if (_data == null) return;

        AnimeStatus? previous = _data.UserStatus;
        AnimeStatus next = StatusEnum.ToAnimeStatus(status.ToString()).TranslatedToAnimeStatus();

        // Optimistic UI; revert on failure.
        _data.UserStatus = status == AnimeStatusApi.none ? null : next;
        HideStatusButtons();
        ShowStatusButtons();
        UpdateMainButtonIcon();

        try
        {
            if (status == AnimeStatusApi.none)
                await _animeService.RemoveFromUserListAsync(_data.AnimeId).ConfigureAwait(true);
            else
                await _animeService.SetAnimeStatusAsync(_data.AnimeId, next).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _data.UserStatus = previous;
            UpdateMainButtonIcon();
            await ToastService.Show($"Failed to update status: {ex.Message}").ConfigureAwait(true);
        }
    }
    
    private void UpdateMainButtonIcon()
    {
        (string iconPath, string statusLabel) = _data?.UserStatus switch
        {
            null                    => ("add_icon", "Not in list"),
            AnimeStatus.None        => ("add_icon", "Not in list"),
            AnimeStatus.PlanToWatch => ("calendarAdd_icon", "Plan to Watch"),
            AnimeStatus.Watching    => ("play_icon", "Watching"),
            AnimeStatus.Completed   => ("check_icon", "Completed"),
            AnimeStatus.Dropped     => ("delete_icon", "Dropped"),
            AnimeStatus.OnHold      => ("add_icon", "On Hold"),
            _                       => ("add_icon", "Not in list")
        };
        Uri        uri    = new Uri($"avares://Aniki/Resources/Icons/{iconPath}.svg");
        SvgSource? source = SvgSource.Load(uri.AbsoluteUri, new Uri("avares://Aniki/"));
        MainButtonIcon.Source = new SvgImage { Source = source };
        
        ToolTip.SetTip(MainButton, $"Current status: {statusLabel}");
    }
}