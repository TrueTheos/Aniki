using Aniki.Models;
using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aniki.Services
{
    public interface IMalApiService
    {
        Task<UserData> GetUserDataAsync();
        Task<List<AnimeData>> GetAnimeListAsync(string status = null);
        Task<Bitmap> GetProfileImageAsync(int userId);
    }
}
