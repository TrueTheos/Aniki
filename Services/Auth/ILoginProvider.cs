namespace Aniki.Services.Auth;

internal interface ILoginProvider
{
    internal enum ProviderType {Mal, AniList}
    
    ProviderType Provider { get; }
    string LoginUrl { get; }

    Task SaveTokenAsync(string token);
    Task<string?> LoginAsync(IProgress<string> progressReporter);
    Task<string?> CheckExistingLoginAsync();
    void Logout();
}