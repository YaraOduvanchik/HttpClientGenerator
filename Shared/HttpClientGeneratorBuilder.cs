using System.Text.Json;
using Shared.Handlers;

namespace Shared;

/// <summary>
/// Builder для настройки HTTP клиент генератора с fluent API
/// Позволяет конфигурировать все аспекты HTTP клиентов: таймауты, JSON настройки, handlers, заголовки
/// </summary>
public class HttpClientGeneratorBuilder
{
    private readonly string _baseUrl;
    private JsonSerializerOptions? _jsonOptions;
    private TimeSpan _timeout = TimeSpan.FromSeconds(100);
    private readonly List<Type> _handlerTypes = new();
    private readonly List<Type> _requiredHandlerTypes = new();
    private readonly Dictionary<string, string> _defaultHeaders = new();
    private HttpClientErrorHandlingOptions _errorHandlingOptions = new();
    private RetryPolicyOptions? _retryOptions;

    /// <summary>
    /// Создает новый builder для HTTP клиент генератора
    /// </summary>
    /// <param name="baseUrl">Базовый URL для HTTP запросов</param>
    /// <exception cref="ArgumentNullException">Если baseUrl равен null</exception>
    /// <exception cref="ArgumentException">Если baseUrl некорректный</exception>
    public HttpClientGeneratorBuilder(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("Base URL cannot be null or empty", nameof(baseUrl));

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out _))
            throw new ArgumentException("Base URL must be a valid absolute URI", nameof(baseUrl));

        _baseUrl = baseUrl;
        AddRequiredHandlers();
    }

    /// <summary>
    /// Создает настроенный экземпляр HTTP клиент генератора
    /// </summary>
    /// <returns>Настроенный генератор HTTP клиентов</returns>
    public HttpClientGenerator Create()
    {
        var jsonOptions = _jsonOptions ?? CreateDefaultJsonOptions();

        // Объединяем обязательные и пользовательские handlers
        var allHandlerTypes = _requiredHandlerTypes.Concat(_handlerTypes).ToList();

        var configuration = new HttpClientConfiguration(
            _baseUrl, 
            jsonOptions, 
            _timeout, 
            allHandlerTypes,
            _defaultHeaders,
            _errorHandlingOptions,
            _retryOptions);

        return new HttpClientGenerator(configuration);
    }

    /// <summary>
    /// Добавляет HTTP handler в pipeline обработки запросов
    /// </summary>
    /// <typeparam name="THandler">Тип HTTP handler наследующийся от DelegatingHandler</typeparam>
    /// <returns>Builder для цепочки вызовов</returns>
    /// <exception cref="ArgumentException">Если handler уже добавлен</exception>
    public HttpClientGeneratorBuilder AddHandler<THandler>() where THandler : DelegatingHandler
    {
        var handlerType = typeof(THandler);
        
        if (_handlerTypes.Contains(handlerType))
            throw new ArgumentException($"Handler {handlerType.Name} is already added");

        _handlerTypes.Add(handlerType);
        return this;
    }

    /// <summary>
    /// Добавляет несколько HTTP handlers
    /// </summary>
    /// <param name="handlerTypes">Типы handlers для добавления</param>
    /// <returns>Builder для цепочки вызовов</returns>
    /// <exception cref="ArgumentNullException">Если handlerTypes равен null</exception>
    /// <exception cref="ArgumentException">Если тип не наследуется от DelegatingHandler</exception>
    public HttpClientGeneratorBuilder AddHandlers(params Type[] handlerTypes)
    {
        if (handlerTypes == null)
            throw new ArgumentNullException(nameof(handlerTypes));

        foreach (var handlerType in handlerTypes)
        {
            if (!typeof(DelegatingHandler).IsAssignableFrom(handlerType))
                throw new ArgumentException($"Type {handlerType.Name} must inherit from DelegatingHandler");

            if (!_handlerTypes.Contains(handlerType))
                _handlerTypes.Add(handlerType);
        }

        return this;
    }

    /// <summary>
    /// Добавляет логгирование HTTP запросов и ответов
    /// </summary>
    /// <returns>Builder для цепочки вызовов</returns>
    public HttpClientGeneratorBuilder WithLogging()
    {
        return AddHandler<LoggingHandler>();
    }

    /// <summary>
    /// Настраивает JSON опции по умолчанию (camelCase, case insensitive)
    /// </summary>
    /// <returns>Builder для цепочки вызовов</returns>
    public HttpClientGeneratorBuilder WithDefaultJsonOptions()
    {
        _jsonOptions = CreateDefaultJsonOptions();
        return this;
    }

    /// <summary>
    /// Настраивает кастомные опции JSON сериализации
    /// </summary>
    /// <param name="options">Настройки JSON сериализации</param>
    /// <returns>Builder для цепочки вызовов</returns>
    /// <exception cref="ArgumentNullException">Если options равен null</exception>
    public HttpClientGeneratorBuilder WithJsonOptions(JsonSerializerOptions options)
    {
        _jsonOptions = options ?? throw new ArgumentNullException(nameof(options));
        return this;
    }

    /// <summary>
    /// Настраивает JSON опции с помощью делегата
    /// </summary>
    /// <param name="configure">Делегат для настройки JSON опций</param>
    /// <returns>Builder для цепочки вызовов</returns>
    /// <exception cref="ArgumentNullException">Если configure равен null</exception>
    public HttpClientGeneratorBuilder WithJsonOptions(Action<JsonSerializerOptions> configure)
    {
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        var options = _jsonOptions ?? CreateDefaultJsonOptions();
        configure(options);
        _jsonOptions = options;
        return this;
    }

    /// <summary>
    /// Настраивает таймаут для HTTP запросов
    /// </summary>
    /// <param name="timeout">Таймаут для HTTP запросов</param>
    /// <returns>Builder для цепочки вызовов</returns>
    /// <exception cref="ArgumentException">Если таймаут меньше или равен нулю</exception>
    public HttpClientGeneratorBuilder WithTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentException("Timeout must be positive", nameof(timeout));

        _timeout = timeout;
        return this;
    }

    /// <summary>
    /// Настраивает таймаут для HTTP запросов в секундах
    /// </summary>
    /// <param name="timeoutSeconds">Таймаут в секундах</param>
    /// <returns>Builder для цепочки вызовов</returns>
    /// <exception cref="ArgumentException">Если таймаут меньше или равен нулю</exception>
    public HttpClientGeneratorBuilder WithTimeout(int timeoutSeconds)
    {
        if (timeoutSeconds <= 0)
            throw new ArgumentException("Timeout must be positive", nameof(timeoutSeconds));

        return WithTimeout(TimeSpan.FromSeconds(timeoutSeconds));
    }

    /// <summary>
    /// Добавляет заголовок по умолчанию для всех запросов
    /// </summary>
    /// <param name="name">Имя заголовка</param>
    /// <param name="value">Значение заголовка</param>
    /// <returns>Builder для цепочки вызовов</returns>
    /// <exception cref="ArgumentException">Если имя заголовка пустое</exception>
    /// <exception cref="ArgumentNullException">Если значение заголовка равно null</exception>
    public HttpClientGeneratorBuilder WithDefaultHeader(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Header name cannot be null or empty", nameof(name));

        if (value == null)
            throw new ArgumentNullException(nameof(value));

        _defaultHeaders[name] = value;
        return this;
    }

    /// <summary>
    /// Добавляет несколько заголовков по умолчанию
    /// </summary>
    /// <param name="headers">Словарь заголовков</param>
    /// <returns>Builder для цепочки вызовов</returns>
    /// <exception cref="ArgumentNullException">Если headers равен null</exception>
    public HttpClientGeneratorBuilder WithDefaultHeaders(IDictionary<string, string> headers)
    {
        if (headers == null)
            throw new ArgumentNullException(nameof(headers));

        foreach (var header in headers)
        {
            WithDefaultHeader(header.Key, header.Value);
        }

        return this;
    }

    /// <summary>
    /// Добавляет User-Agent заголовок
    /// </summary>
    /// <param name="userAgent">Значение User-Agent</param>
    /// <returns>Builder для цепочки вызовов</returns>
    /// <exception cref="ArgumentException">Если userAgent пустой</exception>
    public HttpClientGeneratorBuilder WithUserAgent(string userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            throw new ArgumentException("User agent cannot be null or empty", nameof(userAgent));

        return WithDefaultHeader("User-Agent", userAgent);
    }

    /// <summary>
    /// Добавляет Authorization заголовок с Bearer token
    /// </summary>
    /// <param name="token">Bearer token</param>
    /// <returns>Builder для цепочки вызовов</returns>
    /// <exception cref="ArgumentException">Если token пустой</exception>
    public HttpClientGeneratorBuilder WithBearerToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Token cannot be null or empty", nameof(token));

        return WithDefaultHeader("Authorization", $"Bearer {token}");
    }

    /// <summary>
    /// Добавляет API ключ в заголовок
    /// </summary>
    /// <param name="headerName">Имя заголовка для API ключа</param>
    /// <param name="apiKey">API ключ</param>
    /// <returns>Builder для цепочки вызовов</returns>
    /// <exception cref="ArgumentException">Если параметры пустые</exception>
    public HttpClientGeneratorBuilder WithApiKey(string headerName, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(headerName))
            throw new ArgumentException("Header name cannot be null or empty", nameof(headerName));

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));

        return WithDefaultHeader(headerName, apiKey);
    }

    /// <summary>
    /// Настраивает retry политику для HTTP запросов
    /// </summary>
    /// <param name="maxRetries">Максимальное количество повторов</param>
    /// <param name="baseDelay">Базовая задержка между повторами</param>
    /// <param name="useExponentialBackoff">Использовать экспоненциальную задержку</param>
    /// <returns>Builder для цепочки вызовов</returns>
    /// <exception cref="ArgumentException">Если параметры некорректные</exception>
    public HttpClientGeneratorBuilder WithRetry(int maxRetries = 3, TimeSpan? baseDelay = null, bool useExponentialBackoff = true)
    {
        if (maxRetries < 0)
            throw new ArgumentException("Max retries cannot be negative", nameof(maxRetries));

        var delay = baseDelay ?? TimeSpan.FromSeconds(1);
        if (delay <= TimeSpan.Zero)
            throw new ArgumentException("Base delay must be positive", nameof(baseDelay));

        _retryOptions = new RetryPolicyOptions
        {
            MaxRetries = maxRetries,
            BaseDelay = delay,
            UseExponentialBackoff = useExponentialBackoff
        };

        return this;
    }

    /// <summary>
    /// Настраивает retry политику с помощью делегата
    /// </summary>
    /// <param name="configure">Делегат для настройки retry политики</param>
    /// <returns>Builder для цепочки вызовов</returns>
    /// <exception cref="ArgumentNullException">Если configure равен null</exception>
    public HttpClientGeneratorBuilder WithRetry(Action<RetryPolicyOptions> configure)
    {
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        _retryOptions = new RetryPolicyOptions();
        configure(_retryOptions);
        return this;
    }

    /// <summary>
    /// Настраивает обработку ошибок HTTP клиента
    /// </summary>
    /// <param name="configure">Делегат для настройки обработки ошибок</param>
    /// <returns>Builder для цепочки вызовов</returns>
    /// <exception cref="ArgumentNullException">Если configure равен null</exception>
    public HttpClientGeneratorBuilder WithErrorHandling(Action<HttpClientErrorHandlingOptions> configure)
    {
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        configure(_errorHandlingOptions);
        return this;
    }

    /// <summary>
    /// Отключает автоматическое выбрасывание исключений при ошибочных HTTP статусах
    /// </summary>
    /// <returns>Builder для цепочки вызовов</returns>
    public HttpClientGeneratorBuilder WithoutErrorStatusExceptions()
    {
        _errorHandlingOptions.ThrowOnErrorStatus = false;
        return this;
    }

    /// <summary>
    /// Отключает логгирование ошибок
    /// </summary>
    /// <returns>Builder для цепочки вызовов</returns>
    public HttpClientGeneratorBuilder WithoutErrorLogging()
    {
        _errorHandlingOptions.LogErrors = false;
        return this;
    }

    /// <summary>
    /// Добавляет обязательные handlers, которые должны быть зарегистрированы всегда
    /// </summary>
    private void AddRequiredHandlers()
    {
        // ExceptionHandler должен быть первым в pipeline для правильной обработки ошибок
        _requiredHandlerTypes.Add(typeof(ExceptionHandler));
    }

    /// <summary>
    /// Создает настройки JSON по умолчанию
    /// </summary>
    /// <returns>Настройки JSON с параметрами по умолчанию</returns>
    private static JsonSerializerOptions CreateDefaultJsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Проверяет корректность текущей конфигурации
    /// </summary>
    /// <returns>Список найденных проблем конфигурации</returns>
    public IList<string> ValidateConfiguration()
    {
        var issues = new List<string>();

        if (_timeout <= TimeSpan.Zero)
            issues.Add("Timeout must be positive");

        if (_retryOptions?.MaxRetries < 0)
            issues.Add("Max retries cannot be negative");

        if (_retryOptions?.BaseDelay <= TimeSpan.Zero)
            issues.Add("Retry base delay must be positive");

        if (_handlerTypes.GroupBy(t => t).Any(g => g.Count() > 1))
            issues.Add("Duplicate handlers detected");

        return issues;
    }
}
