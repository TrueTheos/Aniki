namespace Aniki.Services.Auth;

public class LoginService : ILoginService
{
    public IReadOnlyList<ILoginProvider> Providers { get; }

    public LoginService(IEnumerable<ILoginProvider> providers)
    {
        Providers = new List<ILoginProvider>(providers);

        DependencyInjection.Instance.OnLogout += OnLogout;
    }

    public ILoginProvider? GetProvider(ILoginProvider.ProviderType id)
    {
        return Providers.FirstOrDefault(p => p.Provider == id);
    }

    private void OnLogout()
    {
        foreach (ILoginProvider provider in Providers)
        {
            provider.Logout();
        }
    }
}