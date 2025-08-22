using System.Net;
using Microsoft.Extensions.Logging;

namespace Shared.Handlers;

/// <summary>
/// Обязательный handler для обработки исключений HTTP запросов
/// Преобразует HTTP ошибки в понятные исключения с детальной информацией
/// </summary>
public class ExceptionHandler : DelegatingHandler
{
    private readonly ILogger<ExceptionHandler> _logger;

    public ExceptionHandler(ILogger<ExceptionHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await base.SendAsync(request, cancellationToken);

            // Проверяем статус код и преобразуем в соответствующие исключения
            if (!response.IsSuccessStatusCode)
            {
                await HandleErrorResponse(request, response);
            }

            return response;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request exception occurred for {Method} {Uri}",
                request.Method, request.RequestUri);
            throw new HttpClientException($"Request to {request.RequestUri} failed", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "HTTP request timeout for {Method} {Uri}",
                request.Method, request.RequestUri);
            throw new HttpClientTimeoutException($"Request to {request.RequestUri} timed out", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "HTTP request was cancelled for {Method} {Uri}",
                request.Method, request.RequestUri);
            throw new HttpClientCancelledException($"Request to {request.RequestUri} was cancelled", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred during HTTP request to {Method} {Uri}",
                request.Method, request.RequestUri);
            throw new HttpClientException($"Unexpected error during request to {request.RequestUri}", ex);
        }
    }

    private async Task HandleErrorResponse(HttpRequestMessage request, HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var errorMessage = $"HTTP {(int)response.StatusCode} {response.StatusCode} - {response.ReasonPhrase}";

        _logger.LogWarning("HTTP error response: {ErrorMessage} for {Method} {Uri}. Content: {Content}",
            errorMessage, request.Method, request.RequestUri, content);

        var exception = response.StatusCode switch
        {
            HttpStatusCode.BadRequest => new HttpClientBadRequestException(errorMessage, content),
            HttpStatusCode.Unauthorized => new HttpClientUnauthorizedException(errorMessage, content),
            HttpStatusCode.Forbidden => new HttpClientForbiddenException(errorMessage, content),
            HttpStatusCode.NotFound => new HttpClientNotFoundException(errorMessage, content),
            HttpStatusCode.Conflict => new HttpClientConflictException(errorMessage, content),
            HttpStatusCode.UnprocessableEntity => new HttpClientValidationException(errorMessage, content),
            HttpStatusCode.InternalServerError => new HttpClientServerException(errorMessage, content),
            HttpStatusCode.BadGateway => new HttpClientServerException(errorMessage, content),
            HttpStatusCode.ServiceUnavailable => new HttpClientServerException(errorMessage, content),
            HttpStatusCode.GatewayTimeout => new HttpClientServerException(errorMessage, content),
            _ => new HttpClientException(errorMessage, content)
        };

        throw exception;
    }
}

/// <summary>
/// Базовое исключение для HTTP клиента
/// </summary>
public class HttpClientException : Exception
{
    public string? ResponseContent { get; }

    public HttpClientException(string message) : base(message) { }
    public HttpClientException(string message, Exception innerException) : base(message, innerException) { }
    public HttpClientException(string message, string? responseContent) : base(message)
    {
        ResponseContent = responseContent;
    }
}

/// <summary>
/// Исключение для HTTP 400 Bad Request
/// </summary>
public class HttpClientBadRequestException : HttpClientException
{
    public HttpClientBadRequestException(string message, string? responseContent) : base(message, responseContent) { }
}

/// <summary>
/// Исключение для HTTP 401 Unauthorized
/// </summary>
public class HttpClientUnauthorizedException : HttpClientException
{
    public HttpClientUnauthorizedException(string message, string? responseContent) : base(message, responseContent) { }
}

/// <summary>
/// Исключение для HTTP 403 Forbidden
/// </summary>
public class HttpClientForbiddenException : HttpClientException
{
    public HttpClientForbiddenException(string message, string? responseContent) : base(message, responseContent) { }
}

/// <summary>
/// Исключение для HTTP 404 Not Found
/// </summary>
public class HttpClientNotFoundException : HttpClientException
{
    public HttpClientNotFoundException(string message, string? responseContent) : base(message, responseContent) { }
}

/// <summary>
/// Исключение для HTTP 409 Conflict
/// </summary>
public class HttpClientConflictException : HttpClientException
{
    public HttpClientConflictException(string message, string? responseContent) : base(message, responseContent) { }
}

/// <summary>
/// Исключение для HTTP 422 Unprocessable Entity
/// </summary>
public class HttpClientValidationException : HttpClientException
{
    public HttpClientValidationException(string message, string? responseContent) : base(message, responseContent) { }
}

/// <summary>
/// Исключение для HTTP 5xx Server Errors
/// </summary>
public class HttpClientServerException : HttpClientException
{
    public HttpClientServerException(string message, string? responseContent) : base(message, responseContent) { }
}

/// <summary>
/// Исключение для таймаута HTTP запроса
/// </summary>
public class HttpClientTimeoutException : HttpClientException
{
    public HttpClientTimeoutException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Исключение для отмененного HTTP запроса
/// </summary>
public class HttpClientCancelledException : HttpClientException
{
    public HttpClientCancelledException(string message, Exception innerException) : base(message, innerException) { }
}
