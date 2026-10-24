using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace Aniki.Views;

internal sealed partial class AnimeDetailsView : UserControl
{
    private readonly AnimeDetailsViewModel _viewModel;
    private int? _shownId;

    private void GoToBrowse(object? sender, RoutedEventArgs e)
    {
        DependencyInjection.Instance.ServiceProvider!.GetRequiredService<MainViewModel>().ShowAnimeBrowsePageCommand.Execute(null);
    }

    public AnimeDetailsView()
    {
        InitializeComponent();
        
        _viewModel = DependencyInjection.Instance.ServiceProvider!.GetRequiredService<AnimeDetailsViewModel>();

        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(AnimeDetailsViewModel.Details) || _viewModel.Details?.Id == _shownId) return;
            _shownId = _viewModel.Details?.Id;
            this.FindControl<ScrollViewer>("DetailsScroll")!.Offset = default;
        };

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
        _viewModel.CopyAnimeTitleCommand.Execute(null);
    }
}
