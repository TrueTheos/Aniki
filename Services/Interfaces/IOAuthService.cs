namespace Aniki.Services.Interfaces;

public interface IOAuthService
{
    public Task<bool> StartOAuthFlowAsync(IProgress<string> progressReporter);
}