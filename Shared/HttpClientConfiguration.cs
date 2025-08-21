using System.Text.Json;

namespace Shared;

/// <summary>
/// Конфигурация для HTTP клиента, содержащая все необходимые настройки
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
    /// Создает новую конфигурацию HTTP клиента
    /// </summary>
    /// <param name="baseUrl">Базовый URL для HTTP запросов</param>
    /// <param name="jsonOptions">Настройки JSON сериализации</param>
    /// <param name="timeout">Таймаут для HTTP запросов</param>
    /// <param name="handlerTypes">Типы handlers для HTTP pipeline</param>
    /// <exception cref="ArgumentNullException">Если baseUrl равен null</exception>
    public HttpClientConfiguration(
        string baseUrl,
        JsonSerializerOptions? jsonOptions,
        TimeSpan timeout,
        IEnumerable<Type> handlerTypes)
    {
        BaseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        JsonOptions = jsonOptions;
        Timeout = timeout;
        HandlerTypes = handlerTypes.ToList().AsReadOnly();
    }
}
