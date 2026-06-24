using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace Aniki.Views;

public partial class CalendarView : UserControl
{
    private readonly CalendarViewModel _viewModel;
    
    public CalendarView()
    {
        InitializeComponent();
        
        _viewModel = DependencyInjection.Instance.ServiceProvider!.GetRequiredService<CalendarViewModel>();
    }

    private async void OnAnimeTapped(object? sender, TappedEventArgs _)
    {
        try
        {
            if (sender is StackPanel { DataContext: AnimeScheduleItem asi })
            {
                await _viewModel.GoToClickedAnime(asi);
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
    }

    private void InitializeComponent()
        => AvaloniaXamlLoader.Load(this);
}