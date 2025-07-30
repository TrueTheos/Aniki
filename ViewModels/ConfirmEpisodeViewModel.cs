namespace Aniki.ViewModels
{
    public partial class ConfirmEpisodeViewModel : ViewModelBase
    {
        public int EpisodeNumber { get; }

        public string Message => $"Mark Episode {EpisodeNumber} as completed?";

        public ConfirmEpisodeViewModel() { }

        public ConfirmEpisodeViewModel(int episodeNumber)
        {
            EpisodeNumber = episodeNumber;
        }
    }
}
