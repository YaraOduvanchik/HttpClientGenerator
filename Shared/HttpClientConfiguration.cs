using System.Text.Json;

namespace Shared;

/// <summary>
/// Конфигурация для HTTP клиента, содержащая все необходимые настройки
/// Включает базовый URL, настройки JSON, таймауты и список handlers
/// </summary>
internal class HttpClientConfiguration
{
    /// <summary>
    /// Базовый URL для HTTP запросов
    /// </summary>
    public string BaseUrl { get; }

    /// <summary>
    /// Настройки JSON сериализации/десериализации
    /// </summary>
    public JsonSerializerOptions? JsonOptions { get; }

    /// <summary>
    /// Таймаут для HTTP запросов
    /// </summary>
    public TimeSpan Timeout { get; }

    /// <summary>
    /// Типы HTTP handlers для регистрации в pipeline
    /// </summary>
    public IReadOnlyList<Type> HandlerTypes { get; }

    /// <summary>
    /// Заголовки по умолчанию для всех запросов
    /// </summary>
    public IReadOnlyDictionary<string, string> DefaultHeaders { get; }

    /// <summary>
    /// Настройки обработки ошибок
    /// </summary>
    public HttpClientErrorHandlingOptions ErrorHandlingOptions { get; }

    /// <summary>
    /// Настройки retry политики
    /// </summary>
    public RetryPolicyOptions? RetryOptions { get; }

    /// <summary>
    /// Создает новую конфигурацию HTTP клиента
    /// </summary>
    /// <param name="baseUrl">Базовый URL для HTTP запросов</param>
    /// <param name="jsonOptions">Настройки JSON сериализации</param>
    /// <param name="timeout">Таймаут для HTTP запросов</param>
    /// <param name="handlerTypes">Типы handlers для HTTP pipeline</param>
    /// <param name="defaultHeaders">Заголовки по умолчанию</param>
    /// <param name="errorHandlingOptions">Настройки обработки ошибок</param>
    /// <param name="retryOptions">Настройки retry политики</param>
    /// <exception cref="ArgumentNullException">Если baseUrl равен null</exception>
    /// <exception cref="ArgumentException">Если baseUrl некорректный</exception>
    public HttpClientConfiguration(
        string baseUrl,
        JsonSerializerOptions? jsonOptions,
        TimeSpan timeout,
        IEnumerable<Type> handlerTypes,
        IDictionary<string, string>? defaultHeaders = null,
        HttpClientErrorHandlingOptions? errorHandlingOptions = null,
        RetryPolicyOptions? retryOptions = null)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("Base URL cannot be null or empty", nameof(baseUrl));

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out _))
            throw new ArgumentException("Base URL must be a valid absolute URI", nameof(baseUrl));

        BaseUrl = baseUrl.TrimEnd('/');
        JsonOptions = jsonOptions;
        Timeout = timeout > TimeSpan.Zero ? timeout : throw new ArgumentException("Timeout must be positive", nameof(timeout));
        HandlerTypes = handlerTypes.ToList().AsReadOnly();
        DefaultHeaders = (defaultHeaders ?? new Dictionary<string, string>()).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        ErrorHandlingOptions = errorHandlingOptions ?? new HttpClientErrorHandlingOptions();
        RetryOptions = retryOptions;
    }
}

/// <summary>
/// Настройки обработки ошибок HTTP клиента
/// </summary>
public class HttpClientErrorHandlingOptions
{
    /// <summary>
    /// Выбрасывать исключения при ошибочных HTTP статусах (4xx, 5xx)
    /// </summary>
    public bool ThrowOnErrorStatus { get; set; } = true;

    /// <summary>
    /// Оборачивать системные исключения в кастомные HttpClientException
    /// </summary>
    public bool WrapExceptions { get; set; } = true;

    /// <summary>
    /// Логгировать ошибки HTTP запросов
    /// </summary>
    public bool LogErrors { get; set; } = true;

    /// <summary>
    /// Логгировать содержимое ответа при ошибках
    /// </summary>
    public bool LogErrorResponseContent { get; set; } = false;

    /// <summary>
    /// Максимальный размер содержимого ответа для логгирования (в байтах)
    /// </summary>
    public int MaxLoggedResponseSize { get; set; } = 1024;
}

/// <summary>
/// Настройки retry политики для HTTP запросов
/// </summary>
public class RetryPolicyOptions
{
    /// <summary>
    /// Максимальное количество попыток повтора
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Базовая задержка между попытками
    /// </summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Использовать экспоненциальную задержку
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Максимальная задержка между попытками
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// HTTP статус коды, при которых следует повторять запрос
    /// </summary>
    public HashSet<int> RetryStatusCodes { get; set; } = new() { 500, 502, 503, 504 };

    /// <summary>
    /// Повторять запросы при timeout исключениях
    /// </summary>
    public bool RetryOnTimeout { get; set; } = true;

    /// <summary>
    /// Повторять запросы при network исключениях
    /// </summary>
    public bool RetryOnNetworkError { get; set; } = true;
}
