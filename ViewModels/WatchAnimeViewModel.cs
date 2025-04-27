using Aniki.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aniki.ViewModels
{
    public partial class WatchAnimeViewModel : ViewModelBase
    {
        private AnimeDetails _details;

        public WatchAnimeViewModel() { }

        public WatchAnimeViewModel(AnimeDetails details)
        {
            _details = details;
        }
    }
}
