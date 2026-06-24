using System.Collections.ObjectModel;

namespace Aniki.ViewModels;

public partial class ConfirmEpisodeViewModel : ViewModelBase
{
    [ObservableProperty] 
    private int _episodeNumber;

    [ObservableProperty]
    private ObservableCollection<int> _episodeNumbers;

    public ConfirmEpisodeViewModel(int episodeNumber, int maxEpisodes)
    {
        EpisodeNumber = episodeNumber; 

        EpisodeNumbers = new ObservableCollection<int>(Enumerable.Range(1, Math.Max(maxEpisodes, episodeNumber)));
    }
}