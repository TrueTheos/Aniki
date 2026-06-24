using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Aniki.Views;

public partial class MainWindow : Window
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

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void MainWindow_Loaded(object? sender, RoutedEventArgs routedEventArgs)
    {
        try
        {
            await _viewModel.InitializeAsync();
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
            await settingsWindow.ShowDialog(this);
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