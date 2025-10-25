using System.Collections.Concurrent;
using System.Security.Claims;

namespace Coflnet.Connections.Middleware;

/// <summary>
/// Rate limiting middleware to prevent abuse
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly ConcurrentDictionary<string, UserRateLimit> _rateLimits = new();
    private readonly int _maxRequestsPerMinute;

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _maxRequestsPerMinute = configuration.GetValue<int>("RateLimit:MaxRequestsPerMinute", 60);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        // Skip rate limiting for unauthenticated requests (handled by auth)
        if (string.IsNullOrEmpty(userId))
        {
            await _next(context);
            return;
        }

        var rateLimit = _rateLimits.GetOrAdd(userId, _ => new UserRateLimit());

        if (!rateLimit.AllowRequest(_maxRequestsPerMinute))
        {
            _logger.LogWarning("Rate limit exceeded for user {UserId}", userId);
            context.Response.StatusCode = 429; // Too Many Requests
            context.Response.Headers["Retry-After"] = "60";
            await context.Response.WriteAsJsonAsync(new
            {
                Error = "Rate limit exceeded",
                Message = $"Maximum {_maxRequestsPerMinute} requests per minute allowed",
                RetryAfter = 60
            });
            return;
        }

        await _next(context);
    }

    private class UserRateLimit
    {
        private readonly ConcurrentQueue<DateTime> _requests = new();
        private readonly object _lock = new();

        public bool AllowRequest(int maxRequestsPerMinute)
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                var oneMinuteAgo = now.AddMinutes(-1);

                // Remove old requests
                while (_requests.TryPeek(out var oldest) && oldest < oneMinuteAgo)
                {
                    _requests.TryDequeue(out _);
                }

                if (_requests.Count >= maxRequestsPerMinute)
                {
                    return false;
                }

                _requests.Enqueue(now);
                return true;
            }
        }
    }
}

/// <summary>
/// Extension methods for rate limiting middleware
/// </summary>
public static class RateLimitingMiddlewareExtensions
{
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RateLimitingMiddleware>();
    }
}
