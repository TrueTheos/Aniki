using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Aniki.Views;

internal sealed partial class ConfirmEpisodeWindow : Window
{
    public ConfirmEpisodeWindow()
    {
        InitializeComponent();
    }

    private void OnYes(object? sender, RoutedEventArgs e)
        => Close(true);

    private void OnNo(object? sender, RoutedEventArgs e)
        => Close(false);
}