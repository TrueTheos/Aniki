using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace Aniki.Views;

public partial class CalendarView : UserControl
{
    public CalendarView()
    {
        InitializeComponent();
    }

    private async void OnAnimeTapped(object? sender, TappedEventArgs _)
    {
        if (sender is StackPanel { DataContext: AnimeScheduleItem asi } &&
            DataContext is CalendarViewModel vm)
        {
            await vm.GoToClickedAnime(asi);
        }
    }

    private void InitializeComponent()
        => AvaloniaXamlLoader.Load(this);
}