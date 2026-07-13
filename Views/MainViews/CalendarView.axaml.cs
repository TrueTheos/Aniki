using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace Aniki.Views;

internal sealed partial class CalendarView : UserControl
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
                await CalendarViewModel.GoToClickedAnime(asi).ConfigureAwait(true);
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