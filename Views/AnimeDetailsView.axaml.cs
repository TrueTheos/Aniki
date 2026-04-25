using Aniki.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace Aniki.Views;

public partial class AnimeDetailsView : UserControl
{
    private TextBox? _episodesInputBox;

    public AnimeDetailsView()
    {
        InitializeComponent();

        _episodesInputBox = this.FindControl<TextBox>("EpisodesInputBox");
        _episodesInputBox?.GetObservable(IsVisibleProperty).Subscribe(visible =>
        {
            if (!visible || _episodesInputBox == null) return;

            Dispatcher.UIThread.Post(() =>
            {
                _episodesInputBox.Focus();
                _episodesInputBox.SelectAll();
            });
        });
    }

    private void InitializeComponent()
        => AvaloniaXamlLoader.Load(this);

    private void OnEpisodesInputLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AnimeDetailsViewModel vm && vm.IsEditingEpisodes)
        {
            vm.CommitEpisodesInputCommand.Execute(null);
        }
    }
}
