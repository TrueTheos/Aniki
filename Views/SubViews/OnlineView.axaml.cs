using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace Aniki.Views;

internal sealed partial class OnlineView : UserControl
{
    private readonly OnlineViewModel _viewModel;
    
    public OnlineView()
    {
        InitializeComponent();
        
        _viewModel = DependencyInjection.Instance.ServiceProvider!.GetRequiredService<OnlineViewModel>();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        if (_viewModel.SearchAnimeCommand.CanExecute(null))
        {
            _viewModel.SearchAnimeCommand.Execute(null);
            e.Handled = true;
        }
    }
}
