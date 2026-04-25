using Aniki.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace Aniki.Views;

public partial class OnlineView : UserControl
{
    public OnlineView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        if (DataContext is OnlineViewModel vm && vm.SearchAnimeCommand.CanExecute(null))
        {
            vm.SearchAnimeCommand.Execute(null);
            e.Handled = true;
        }
    }
}
