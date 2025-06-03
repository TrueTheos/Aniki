using Aniki.Models;
using Aniki.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;

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
            if (lb.SelectedItem == null) return;
            if (DataContext is CalendarViewModel vm)
            {
                vm.GoToClickedAnime(lb.SelectedItem as AnimeScheduleItem);
            }
        }
    }

    private void InitializeComponent()
        => AvaloniaXamlLoader.Load(this);
}