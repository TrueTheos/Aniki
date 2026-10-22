using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;

namespace Aniki.Views;

internal sealed partial class ReadView : UserControl
{
    private TopLevel? _topLevel;
    private NativeWebView? _web;

    public ReadView()
    {
        InitializeComponent();
        PagesScroller.ScrollChanged += (_, _) => UpdateCurrentPage();
    }

    private List<double> PageTops()
    {
        List<double> tops = [];
        for (int i = 0; i < PagesItems.ItemCount; i++)
        {
            if (PagesItems.ContainerFromIndex(i)?.TranslatePoint(default, PagesPanel) is not { } p) break;
            tops.Add(p.Y);
        }
        return tops;
    }

    private void UpdateCurrentPage()
    {
        if (DataContext is not ReadViewModel vm) return;
        double line = PagesScroller.Offset.Y + PagesScroller.Viewport.Height / 3;
        vm.CurrentPage = Math.Max(1, PageTops().FindLastIndex(t => t <= line) + 1);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is ReadViewModel vm)
        {
            vm.PropertyChanged += OnViewModelChanged;
            UpdateWebReader(vm);
        }
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (DataContext is ReadViewModel vm) vm.PropertyChanged -= OnViewModelChanged;
        _topLevel?.RemoveHandler(KeyDownEvent, OnKeyDown);
        _topLevel = null;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ReadViewModel.Pages))
            PagesScroller.Offset = default;
        if (e.PropertyName == nameof(ReadViewModel.SelectedChapter) && sender is ReadViewModel vm)
            UpdateWebReader(vm);
    }

    private void UpdateWebReader(ReadViewModel vm)
    {
        if (!vm.ShowWebReader || vm.SelectedChapter?.ExternalUrl is not { } url)
        {
            WebHost.IsVisible = false;
            return;
        }

        if (_web == null)
        {
            _web = new NativeWebView();
            _web.EnvironmentRequested += (_, args) =>
            {
                if (args is WindowsWebView2EnvironmentRequestedEventArgs webView2)
                    webView2.UserDataFolder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aniki", "WebView");
            };
            _web.NewWindowRequested += (_, args) => args.Handled = true;
            WebHost.Children.Add(_web);
        }

        WebHost.IsVisible = true;
        _web.Navigate(new Uri(url));
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ReadViewModel { Manga: not null } vm || e.Source is TextBox) return;

        switch (e.Key)
        {
            case Key.Right: TurnPage(vm, +1); break;
            case Key.Left: TurnPage(vm, -1); break;
            case Key.F or Key.F11: vm.ToggleFullscreenCommand.Execute(null); break;
            case Key.Escape when vm.IsFullscreen: vm.IsFullscreen = false; break;
            default: return;
        }
        e.Handled = true;
    }

    private void TurnPage(ReadViewModel vm, int direction)
    {
        List<double> tops = PageTops();
        if (tops.Count == 0)
        {
            (direction > 0 ? vm.SkipChapterCommand : vm.PreviousChapterCommand).Execute(null);
            return;
        }

        double y = PagesScroller.Offset.Y;
        int current = tops.FindLastIndex(t => t <= y + 2);
        int target = direction > 0 ? current + 1
            : current >= 0 && y > tops[current] + 2 ? current
            : current - 1;

        if (target >= tops.Count)
        {
            double bottom = PagesScroller.Extent.Height - PagesScroller.Viewport.Height;
            if (y < bottom - 2) PagesScroller.Offset = new Vector(PagesScroller.Offset.X, bottom);
            else vm.NextChapterCommand.Execute(null);
            return;
        }
        if (target < 0)
        {
            vm.PreviousChapterCommand.Execute(null);
            return;
        }
        PagesScroller.Offset = new Vector(PagesScroller.Offset.X, tops[target]);
    }
}
