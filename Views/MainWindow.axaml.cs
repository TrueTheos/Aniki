using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Aniki.Views;

internal sealed partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        _viewModel = DependencyInjection.Instance.ServiceProvider!.GetRequiredService<MainViewModel>();
        
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        
        _viewModel.LogoutRequested += OnLogoutRequested;
        _viewModel.SettingsRequested += OnSettingsRequested;
        _viewModel.ReadViewMode.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ReadViewModel.IsFullscreen))
                ApplyReaderFullscreen(_viewModel.ReadViewMode.IsFullscreen);
        };

        DataContext = _viewModel;

        Loaded += MainWindow_Loaded;
        
        string? version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? null;
        if (version != null)
        {
            Title = $"Aniki - {version}";
        }
        else
        {
            Title = "Aniki";
        }

        WindowState = WindowState.Maximized;
    }

    private WindowState _stateBeforeFullscreen = WindowState.Maximized;

    private void ApplyReaderFullscreen(bool on)
    {
        Grid sidebar = this.FindControl<Grid>("Sidebar")!;
        Border content = this.FindControl<Border>("ContentHost")!;
        StackPanel arrows = this.FindControl<StackPanel>("NavArrows")!;
        arrows.Opacity = on ? 0 : 1;
        arrows.IsHitTestVisible = !on;

        if (on) _stateBeforeFullscreen = WindowState;
        WindowState = on ? WindowState.FullScreen : _stateBeforeFullscreen;
        sidebar.IsVisible = !on;
        ((Grid)sidebar.Parent!).ColumnDefinitions[0].Width = new GridLength(on ? 0 : 220);
        content.BorderThickness = new Thickness(on ? 0 : 1, 0, 0, 0);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void MainWindow_Loaded(object? sender, RoutedEventArgs routedEventArgs)
    {
        try
        {
            await _viewModel.InitializeAsync().ConfigureAwait(true);
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
    }

    private void OnLogoutRequested(object? sender, EventArgs e)
    {
        LogOut(sender, new RoutedEventArgs());
    }
    
    private void OnBeginDrag(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized 
                ? WindowState.Normal 
                : WindowState.Maximized;
            return;
        }
        BeginMoveDrag(e);
    }

    private async void OnSettingsRequested(object? sender, EventArgs _)
    {
        try
        {
            SettingsViewModel settingsViewModel = DependencyInjection.Instance.ServiceProvider!.GetRequiredService<SettingsViewModel>();
            settingsViewModel.LoadSettings();
            SettingsWindow settingsWindow = new()
            {
                DataContext = settingsViewModel
            };
            await settingsWindow.ShowDialog(this).ConfigureAwait(true);
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
    }
    
    public void LogOut(object? sender, RoutedEventArgs routedEventArgs)
    {
        if (Application.Current is App app)
        {
            app.Reset();
        }
    }
}