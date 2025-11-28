using System.Reflection;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;

namespace Aniki.Views;

public partial class MainWindow : Window
{
    private MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif

        _viewModel = viewModel;
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
        await _viewModel.InitializeAsync();
    }

    private void OnLogoutRequested(object? sender, EventArgs e)
    {
        LogOut(sender, new RoutedEventArgs());
    }
    
    private void OnBeginDrag(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private async void OnSettingsRequested(object? sender, EventArgs e)
    {
        var settingsViewModel = App.ServiceProvider.GetRequiredService<SettingsViewModel>();
        settingsViewModel.LoadSettings();
        SettingsWindow settingsWindow = new()
        {
            DataContext = settingsViewModel
        };
        await settingsWindow.ShowDialog(this);
    }
    
    public void LogOut(object? sender, RoutedEventArgs routedEventArgs)
    {
        if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var loginViewModel = App.ServiceProvider.GetRequiredService<LoginViewModel>();
            loginViewModel.Logout();
            
            desktop.MainWindow = new LoginWindow();
            desktop.MainWindow.Show();
            
            Close();
        }
    }
    
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        
    }
}