using System.Net.Http.Headers;

namespace Aniki.Misc;

class AnilistRateLimitHandler : DelegatingHandler
{
    private static readonly SemaphoreSlim _semaphore = new(1, 1);
    
    private int _remainingRequests = 10;
    private DateTimeOffset _resetTime = DateTimeOffset.MinValue;
    
    private readonly Queue<DateTimeOffset> _requestTimestamps = new();
    private const int MaxBurstRequests = 4;
    private static readonly TimeSpan BurstWindow = TimeSpan.FromSeconds(1);
    
    public AnilistRateLimitHandler(HttpMessageHandler innerHandler) : base(innerHandler)
    {
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            
            if (_remainingRequests <= 0 && _resetTime > now)
            {
                TimeSpan delay = _resetTime - now;
                TimeSpan finalDelay = delay.Add(TimeSpan.FromSeconds(1));

                if (finalDelay.TotalMilliseconds > 0)
                {
                    Console.WriteLine($"Rate limit reached. Waiting {finalDelay.TotalSeconds} seconds.");
                    await Task.Delay(finalDelay, cancellationToken);
                }
            }
            
            while (_requestTimestamps.Count > 0 && _requestTimestamps.Peek() < now - BurstWindow)
            {
                _requestTimestamps.Dequeue();
            }
            
            if (_requestTimestamps.Count >= MaxBurstRequests)
            {
                DateTimeOffset oldestRequest = _requestTimestamps.Peek();
                TimeSpan burstDelay = (oldestRequest + BurstWindow) - now;
                
                if (burstDelay.TotalMilliseconds > 0)
                {
                    Console.WriteLine($"Burst limit reached. Waiting {burstDelay.TotalMilliseconds:F0}ms.");
                    await Task.Delay(burstDelay, cancellationToken);
                    
                    _requestTimestamps.Dequeue();
                }
            }
            
            _requestTimestamps.Enqueue(DateTimeOffset.UtcNow);
        }
        finally
        {
            _semaphore.Release();
        }

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == (System.Net.HttpStatusCode)429)
        {
            TimeSpan? retryAfter = response.Headers.RetryAfter?.Delta;
            if (retryAfter.HasValue)
            {
                Console.WriteLine($"429 received. Retrying after {retryAfter.Value.TotalSeconds:F1} seconds.");
                await Task.Delay(retryAfter.Value, cancellationToken);
                return await base.SendAsync(request, cancellationToken);
            }
        }

        UpdateRateLimits(response.Headers);

        return response;
    }

    private void UpdateRateLimits(HttpResponseHeaders headers)
    {
        if (headers.TryGetValues("X-RateLimit-Remaining", out IEnumerable<string>? remainingValues))
        {
            if (int.TryParse(remainingValues.FirstOrDefault(), out int remaining))
            {
                _remainingRequests = remaining;
                Console.WriteLine($"Rate limit remaining {_remainingRequests}");
            }
        }

        if (headers.TryGetValues("X-RateLimit-Reset", out IEnumerable<string>? resetValues))
        {
            if (long.TryParse(resetValues.FirstOrDefault(), out long resetUnix))
            {
                _resetTime = DateTimeOffset.FromUnixTimeSeconds(resetUnix);
                Console.WriteLine($"Rate limit reset time {_resetTime}");
            }
        }
    }
}