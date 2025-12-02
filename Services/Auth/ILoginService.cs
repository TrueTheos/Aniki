namespace Aniki.Services.Auth;

public interface ILoginService
{
    IReadOnlyList<ILoginProvider> Providers { get; }
    ILoginProvider? GetProvider(ILoginProvider.ProviderType id);
}