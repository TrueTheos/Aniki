using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aniki.Models
{
    public class NyaaTorrent
    {
        public string Title { get; set; }
        public string TorrentLink { get; set; }
        public string Size { get; set; }
        public int Seeders { get; set; }
    }
}
