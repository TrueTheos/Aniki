using System.Collections.ObjectModel;

namespace Aniki.ViewModels;

internal sealed partial class ConfirmEpisodeViewModel : ViewModelBase
{
    [ObservableProperty] public partial int EpisodeNumber { get; set; }

    [ObservableProperty] public partial ObservableCollection<int> EpisodeNumbers { get; set; }

    public ConfirmEpisodeViewModel(int episodeNumber, int maxEpisodes)
    {
        EpisodeNumber = episodeNumber; 

        EpisodeNumbers = new ObservableCollection<int>(Enumerable.Range(1, Math.Max(maxEpisodes, episodeNumber)));
    }
}