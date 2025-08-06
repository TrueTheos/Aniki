using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Aniki.Views;

public partial class CalendarView : UserControl
{
    public CalendarView()
    {
        InitializeComponent();
    }

    private void DoubleClickAnime(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (sender is ListBox lb )
        {
            if (lb.SelectedItem is not AnimeScheduleItem asi) return;
            if (DataContext is CalendarViewModel vm)
            {
                vm.GoToClickedAnime(asi);
            }
        }
    }

    private void InitializeComponent()
        => AvaloniaXamlLoader.Load(this);
}