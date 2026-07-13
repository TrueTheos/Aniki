namespace Aniki.Services.Auth;

internal interface ILoginService
{
    IReadOnlyList<ILoginProvider> Providers { get; }
    ILoginProvider? GetProvider(ILoginProvider.ProviderType id);
}