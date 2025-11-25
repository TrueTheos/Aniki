using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;

namespace Aniki.Views;

public partial class LoginWindow : Window
{
    private LoginViewModel _viewModel;

    public LoginWindow()
    {
        _viewModel = App.ServiceProvider.GetRequiredService<LoginViewModel>();
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif

        _viewModel.NavigateToMainRequested += OnNavigateToMainRequested;

        DataContext = _viewModel;

        Loaded += LoginWindow_Loaded;
        Closing += OnClosing;
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        _viewModel.NavigateToMainRequested -= OnNavigateToMainRequested;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void LoginWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        await _viewModel.CheckExistingLoginAsync();
    }

    private void OnNavigateToMainRequested(object? sender, EventArgs e)
    {
        var mainViewModel = App.ServiceProvider.GetRequiredService<MainViewModel>();
        MainWindow mainWindow = new(mainViewModel);
        
        if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = mainWindow;
        }
        mainWindow.Show();
        Close();
    }
}