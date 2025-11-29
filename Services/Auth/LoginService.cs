namespace Aniki.Services.Auth;

public class LoginService : ILoginService
{
    public IReadOnlyList<ILoginProvider> Providers { get; }

    public LoginService(IEnumerable<ILoginProvider> providers)
    {
        Providers = new List<ILoginProvider>(providers);
    }

    public ILoginProvider? GetProvider(string id)
    {
        return Providers.FirstOrDefault(p => p.Id == id);
    }
}