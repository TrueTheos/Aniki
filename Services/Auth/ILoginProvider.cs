namespace Aniki.Services.Auth;

public interface ILoginProvider
{
    public enum ProviderType {MAL, AniList}
    
    ProviderType Provider { get; }
    string LoginUrl { get; }

    Task SaveTokenAsync(string token);
    Task<string?> LoginAsync(IProgress<string> progressReporter);
    Task<string?> CheckExistingLoginAsync();
    void Logout();
}