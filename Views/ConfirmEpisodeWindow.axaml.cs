using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Aniki.Views
{
    public partial class ConfirmEpisodeWindow : Window
    {
        public ConfirmEpisodeWindow()
        {
            InitializeComponent();
        }

        private void OnYes(object? sender, RoutedEventArgs e)
           => this.Close(true);

        private void OnNo(object? sender, RoutedEventArgs e)
            => this.Close(false);
    }
}