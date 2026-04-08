using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Aniki.Views;

public partial class CalendarView : UserControl
{
    public CalendarView()
    {
        InitializeComponent();
    }

    private async void DoubleClickAnime(object? sender, Avalonia.Input.TappedEventArgs _)
    {
        if (sender is ListBox lb )
        {
            if (lb.SelectedItem is not AnimeScheduleItem asi) return;
            if (DataContext is CalendarViewModel vm)
            {
                await vm.GoToClickedAnime(asi);
            }
        }
    }

    private void InitializeComponent()
        => AvaloniaXamlLoader.Load(this);
}