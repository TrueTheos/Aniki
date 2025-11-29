namespace Aniki.Services.Auth;

public interface ILoginProvider
{
    string Name { get; }
    string Id { get; }
    string LoginUrl { get; }

    Task SaveTokenAsync(string token);
    Task<string?> LoginAsync(IProgress<string> progressReporter);
    Task<string?> CheckExistingLoginAsync();
    void Logout();
}