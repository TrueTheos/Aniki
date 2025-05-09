﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
