using System.Diagnostics;
using System.Net.Http.Headers;

namespace Aniki.Misc;

internal sealed class AnilistRateLimitHandler(HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
{
    private static readonly SemaphoreSlim Semaphore = new(1, 1);
    private static int _nextId;
    private static int _waiting;
    private static int _inFlight;

    private int _remainingRequests = 90;
    private DateTimeOffset _resetTime = DateTimeOffset.MinValue;

    private readonly Queue<DateTimeOffset> _requestTimestamps = new();
    private const int MAX_BURST_REQUESTS = 4;
    private static readonly TimeSpan BurstWindow = TimeSpan.FromSeconds(1);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        int id = Interlocked.Increment(ref _nextId);
        string op = AnilistRequestContext.Operation ?? DescribeRequest(request);
        int waiting = Interlocked.Increment(ref _waiting);

        Log(id, $"QUEUE  waiting={waiting} in-flight={Volatile.Read(ref _inFlight)} | {op}");

        Stopwatch total = Stopwatch.StartNew();
        await Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        Interlocked.Decrement(ref _waiting);

        try
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;

            if (_remainingRequests <= 0 && _resetTime > now)
            {
                TimeSpan delay = _resetTime - now;
                TimeSpan finalDelay = delay.Add(TimeSpan.FromSeconds(1));

                if (finalDelay.TotalMilliseconds > 0)
                {
                    Log(id, $"WAIT   rate-limit {finalDelay.TotalSeconds:F1}s (remaining=0, reset={_resetTime:HH:mm:ss}) | {op}");
                    await Task.Delay(finalDelay, cancellationToken).ConfigureAwait(false);
                }
            }

            now = DateTimeOffset.UtcNow;
            while (_requestTimestamps.Count > 0 && _requestTimestamps.Peek() < now - BurstWindow)
            {
                _requestTimestamps.Dequeue();
            }

            if (_requestTimestamps.Count >= MAX_BURST_REQUESTS)
            {
                DateTimeOffset oldestRequest = _requestTimestamps.Peek();
                TimeSpan burstDelay = oldestRequest + BurstWindow - now;

                if (burstDelay.TotalMilliseconds > 0)
                {
                    Log(id, $"WAIT   burst {burstDelay.TotalMilliseconds:F0}ms ({_requestTimestamps.Count}/{MAX_BURST_REQUESTS} in window) | {op}");
                    await Task.Delay(burstDelay, cancellationToken).ConfigureAwait(false);

                    _requestTimestamps.Dequeue();
                }
            }

            _requestTimestamps.Enqueue(DateTimeOffset.UtcNow);
        }
        finally
        {
            Semaphore.Release();
        }

        int inFlight = Interlocked.Increment(ref _inFlight);
        Log(id, $"START  in-flight={inFlight} waiting={Volatile.Read(ref _waiting)} | {op}");

        Stopwatch http = Stopwatch.StartNew();
        try
        {
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == (System.Net.HttpStatusCode)429)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta;
                if (retryAfter.HasValue)
                {
                    Log(id, $"WAIT   429 retry-after {retryAfter.Value.TotalSeconds:F1}s | {op}");
                    await Task.Delay(retryAfter.Value, cancellationToken).ConfigureAwait(false);
                    http.Restart();
                    response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                }
            }

            UpdateRateLimits(response.Headers);
            Log(id, $"DONE   http={http.ElapsedMilliseconds}ms total={total.ElapsedMilliseconds}ms status={(int)response.StatusCode} remaining={_remainingRequests} in-flight={Volatile.Read(ref _inFlight) - 1} | {op}");
            return response;
        }
        catch (Exception ex)
        {
            Log(id, $"FAIL   http={http.ElapsedMilliseconds}ms total={total.ElapsedMilliseconds}ms {ex.GetType().Name}: {ex.Message} | {op}");
            throw;
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
        }
    }

    private void UpdateRateLimits(HttpResponseHeaders headers)
    {
        if (headers.TryGetValues("X-RateLimit-Remaining", out var remainingValues) &&
            int.TryParse(remainingValues.FirstOrDefault(), out int remaining))
        {
            _remainingRequests = remaining;
        }

        if (headers.TryGetValues("X-RateLimit-Reset", out var resetValues) &&
            long.TryParse(resetValues.FirstOrDefault(), out long resetUnix))
        {
            _resetTime = DateTimeOffset.FromUnixTimeSeconds(resetUnix);
        }
    }

    private static string DescribeRequest(HttpRequestMessage request)
    {
        if (request.Content == null) return $"{request.Method} {request.RequestUri?.AbsolutePath}";
        // Body usually already consumed by GraphQL client before SendAsync; fall back to path.
        return $"{request.Method} graphql";
    }

    private static void Log(int id, string message) =>
        Console.WriteLine($"[AL #{id}] {message}");
}
