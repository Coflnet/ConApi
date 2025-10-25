using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Coflnet.Connections.Middleware;

/// <summary>
/// Global exception handling middleware
/// </summary>
public class GlobalExceptionHandler : IExceptionFilter
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        _logger.LogError(context.Exception, 
            "Unhandled exception occurred. Path: {Path}, User: {User}", 
            context.HttpContext.Request.Path,
            context.HttpContext.User?.Identity?.Name ?? "Anonymous");

        var response = new
        {
            Success = false,
            Message = "An error occurred processing your request.",
            Error = context.Exception.Message,
            // Only include stack trace in development
            StackTrace = context.HttpContext.RequestServices
                .GetRequiredService<IWebHostEnvironment>()
                .IsDevelopment() ? context.Exception.StackTrace : null
        };

        context.Result = new ObjectResult(response)
        {
            StatusCode = context.Exception switch
            {
                ArgumentException => 400,
                UnauthorizedAccessException => 401,
                KeyNotFoundException => 404,
                _ => 500
            }
        };

        context.ExceptionHandled = true;
    }
}
