using System.Text.Json;
using Shared.Handlers;

namespace Shared;

/// <summary>
/// Builder для настройки HTTP клиент генератора с fluent API
/// Позволяет конфигурировать все аспекты HTTP клиентов: таймауты, JSON настройки, handlers
/// </summary>
public class HttpClientGeneratorBuilder
{
    private readonly string _baseUrl;
    private JsonSerializerOptions? _jsonOptions;
    private TimeSpan _timeout = TimeSpan.FromSeconds(100);
    private readonly List<Type> _handlerTypes = new();

    /// <summary>
    /// Создает новый builder для HTTP клиент генератора
    /// </summary>
    /// <param name="baseUrl">Базовый URL для HTTP запросов</param>
    /// <exception cref="ArgumentNullException">Если baseUrl равен null</exception>
    public HttpClientGeneratorBuilder(string baseUrl)
    {
        _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
    }

    /// <summary>
    /// Создает настроенный экземпляр HTTP клиент генератора
    /// </summary>
    /// <returns>Настроенный генератор HTTP клиентов</returns>
    public HttpClientGenerator Create()
    {
        var jsonOptions = _jsonOptions ?? CreateDefaultJsonOptions();
        var configuration = new HttpClientConfiguration(_baseUrl, jsonOptions, _timeout, _handlerTypes);
        return new HttpClientGenerator(configuration);
    }

    /// <summary>
    /// Добавляет HTTP handler в pipeline обработки запросов
    /// </summary>
    /// <typeparam name="THandler">Тип HTTP handler наследующийся от DelegatingHandler</typeparam>
    /// <returns>Builder для цепочки вызовов</returns>
    public HttpClientGeneratorBuilder AddHandler<THandler>() where THandler : DelegatingHandler
    {
        _handlerTypes.Add(typeof(THandler));
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
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
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
    /// Настраивает таймаут для HTTP запросов
    /// </summary>
    /// <param name="timeout">Таймаут для HTTP запросов</param>
    /// <returns>Builder для цепочки вызовов</returns>
    public HttpClientGeneratorBuilder WithTimeout(TimeSpan timeout)
    {
        _timeout = timeout;
        return this;
    }

    /// <summary>
    /// Создает настройки JSON по умолчанию
    /// </summary>
    /// <returns>Настройки JSON с параметрами по умолчанию</returns>
    private static JsonSerializerOptions CreateDefaultJsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
