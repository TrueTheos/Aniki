using System.Net.Http.Headers;

namespace Aniki.Misc;

class AnilistRateLimitHandler : DelegatingHandler
{
    private static readonly SemaphoreSlim _semaphore = new(1, 1);
    
    private int _remainingRequests = 10;
    private DateTimeOffset _resetTime = DateTimeOffset.MinValue;
    
    public AnilistRateLimitHandler(HttpMessageHandler innerHandler) : base(innerHandler)
    {
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            
            if (_remainingRequests <= 0 && _resetTime > now)
            {
                var delay = _resetTime - now;
                var finalDelay = delay.Add(TimeSpan.FromSeconds(1));

                if (finalDelay.TotalMilliseconds > 0)
                {
                    Console.WriteLine($"[AnilistService] Rate limit reached. Waiting {finalDelay.TotalSeconds} seconds.");
                    await Task.Delay(finalDelay, cancellationToken);
                }
            }
        }
        finally
        {
            _semaphore.Release();
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == (System.Net.HttpStatusCode)429)
        {
            var retryAfter = response.Headers.RetryAfter?.Delta;
            if (retryAfter.HasValue)
            {
                await Task.Delay(retryAfter.Value, cancellationToken);
                return await base.SendAsync(request, cancellationToken);
            }
        }

        UpdateRateLimits(response.Headers);

        return response;
    }

    private void UpdateRateLimits(HttpResponseHeaders headers)
    {
        if (headers.TryGetValues("X-RateLimit-Remaining", out var remainingValues))
        {
            if (int.TryParse(remainingValues.FirstOrDefault(), out int remaining))
            {
                _remainingRequests = remaining;
                Console.WriteLine($"Rate limit remaining {_remainingRequests}");
            }
        }

        if (headers.TryGetValues("X-RateLimit-Reset", out var resetValues))
        {
            if (long.TryParse(resetValues.FirstOrDefault(), out long resetUnix))
            {
                _resetTime = DateTimeOffset.FromUnixTimeSeconds(resetUnix);
                Console.WriteLine($"Rate limit reset time {_resetTime}");
            }
        }
    }
}