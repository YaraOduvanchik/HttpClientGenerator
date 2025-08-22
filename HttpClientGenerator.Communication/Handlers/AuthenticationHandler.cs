using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HttpClientGenerator.Communication.Handlers;

/// <summary>
/// Пример кастомного handler для добавления аутентификации к HTTP запросам
/// Демонстрирует как можно создавать специфичные для приложения handlers
/// </summary>
public class AuthenticationHandler : DelegatingHandler
{
    private readonly ILogger<AuthenticationHandler> _logger;
    private readonly string _apiKey;

    public AuthenticationHandler(ILogger<AuthenticationHandler> logger, IConfiguration configuration)
    {
        _logger = logger;
        _apiKey = configuration["ApiKey"] ?? "default-api-key";
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Добавляем заголовок аутентификации
        request.Headers.Add("X-API-Key", _apiKey);

        _logger.LogDebug("Added authentication header to request {Method} {Uri}",
            request.Method, request.RequestUri);

        return await base.SendAsync(request, cancellationToken);
    }
}

/// <summary>
/// Пример еще одного кастомного handler для добавления correlation ID
/// </summary>
public class CorrelationIdHandler : DelegatingHandler
{
    private readonly ILogger<CorrelationIdHandler> _logger;

    public CorrelationIdHandler(ILogger<CorrelationIdHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Генерируем или получаем correlation ID
        var correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString();

        request.Headers.Add("X-Correlation-ID", correlationId);

        _logger.LogDebug("Added correlation ID {CorrelationId} to request {Method} {Uri}",
            correlationId, request.Method, request.RequestUri);

        return await base.SendAsync(request, cancellationToken);
    }
}
