using Aniki.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aniki.Services
{
    public interface ITokenService
    {
        Task<StoredTokenData> LoadTokensAsync();
        Task SaveTokensAsync(TokenResponse tokenResponse);
        void ClearTokens();
        bool HasValidToken();
        string GetAccessToken();
    }
}
