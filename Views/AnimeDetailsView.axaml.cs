using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace Aniki.Views;

public partial class AnimeDetailsView : UserControl
{
    private readonly AnimeDetailsViewModel _viewModel;
    
    public AnimeDetailsView()
    {
        InitializeComponent();
        
        _viewModel = DependencyInjection.Instance.ServiceProvider!.GetRequiredService<AnimeDetailsViewModel>();

        TextBox? episodesInputBox = this.FindControl<TextBox>("EpisodesInputBox");
        episodesInputBox?.GetObservable(IsVisibleProperty).Subscribe(visible =>
        {
            if (!visible) return;

            Dispatcher.UIThread.Post(() =>
            {
                episodesInputBox.Focus();
                episodesInputBox.SelectAll();
            });
        });
    }

    private void InitializeComponent()
        => AvaloniaXamlLoader.Load(this);

    private void OnEpisodesInputLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.IsEditingEpisodes)
        {
            _viewModel.CommitEpisodesInputCommand.Execute(null);
        }
    }

    private void OnTitleTap(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.IsEditingEpisodes)
        {
            _viewModel.CopyAnimeTitleCommand.Execute(null);
        }
    }
}
