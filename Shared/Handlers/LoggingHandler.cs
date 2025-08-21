using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Shared.Handlers;

/// <summary>
/// Handler для логгирования HTTP запросов и ответов
/// </summary>
public class LoggingHandler : DelegatingHandler
{
    private readonly ILogger<LoggingHandler> _logger;

    public LoggingHandler(ILogger<LoggingHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("HTTP {Method} {Uri} - Starting request",
            request.Method, request.RequestUri);

        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            stopwatch.Stop();

            _logger.LogInformation("HTTP {Method} {Uri} - Completed in {ElapsedMs}ms with status {StatusCode}",
                request.Method, request.RequestUri, stopwatch.ElapsedMilliseconds, response.StatusCode);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "HTTP {Method} {Uri} - Failed after {ElapsedMs}ms",
                request.Method, request.RequestUri, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
