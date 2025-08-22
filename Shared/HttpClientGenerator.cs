using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Collections.Concurrent;

namespace Shared;

/// <summary>
/// Генератор HTTP клиентов на основе интерфейсов с HTTP атрибутами.
/// Автоматически создает реализации интерфейсов, анализируя методы с атрибутами HttpGet, HttpPost и т.д.
/// Поддерживает кеширование метаданных для улучшения производительности
/// </summary>
public class HttpClientGenerator
{
    private readonly HttpClientConfiguration _configuration;
    
    // Кеш для хранения метаданных интерфейсов
    private readonly ConcurrentDictionary<Type, InterfaceMetadata> _interfaceCache = new();

    /// <summary>
    /// Метаданные интерфейса для кеширования
    /// </summary>
    public class InterfaceMetadata
    {
        public MethodInfo[] Methods { get; init; } = [];
        public bool IsValidHttpInterface { get; init; }
        public string InterfaceName { get; init; } = string.Empty;
    }

    /// <summary>
    /// Создает новый экземпляр генератора с указанной конфигурацией
    /// </summary>
    /// <param name="configuration">Конфигурация HTTP клиента</param>
    /// <exception cref="ArgumentNullException">Если конфигурация равна null</exception>
    internal HttpClientGenerator(HttpClientConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Создает builder для настройки генератора HTTP клиентов
    /// </summary>
    /// <param name="baseUrl">Базовый URL для HTTP запросов</param>
    /// <returns>Builder для fluent конфигурации</returns>
    /// <exception cref="ArgumentException">Если baseUrl некорректный</exception>
    public static HttpClientGeneratorBuilder BuildForUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("Base URL cannot be null or empty", nameof(baseUrl));

        return new HttpClientGeneratorBuilder(baseUrl);
    }

    /// <summary>
    /// Регистрирует типизированный HTTP клиент для указанного интерфейса в DI контейнере
    /// </summary>
    /// <typeparam name="TInterface">Тип интерфейса с HTTP атрибутами</typeparam>
    /// <param name="services">Коллекция сервисов DI</param>
    /// <param name="clientName">Имя HTTP клиента (по умолчанию - имя интерфейса)</param>
    /// <returns>Builder для дополнительной настройки HTTP клиента</returns>
    /// <exception cref="ArgumentNullException">Если services равен null</exception>
    /// <exception cref="ArgumentException">Если TInterface не является интерфейсом или не содержит HTTP методов</exception>
    public IHttpClientBuilder Register<TInterface>(IServiceCollection services, string? clientName = null)
        where TInterface : class
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        ValidateAndCacheInterface<TInterface>();

        var name = clientName ?? GenerateClientName<TInterface>();

        var httpClientBuilder = services
            .AddHttpClient(name, ConfigureHttpClient)
            .AddTypedClient(CreateTypedClient<TInterface>);

        RegisterHandlers(httpClientBuilder);
        return httpClientBuilder;
    }

    /// <summary>
    /// Создает экземпляр HTTP клиента для указанного интерфейса без регистрации в DI
    /// </summary>
    /// <typeparam name="TInterface">Тип интерфейса с HTTP атрибутами</typeparam>
    /// <param name="httpClient">Настроенный HttpClient</param>
    /// <param name="logger">Логгер (опционально)</param>
    /// <returns>Реализация интерфейса через динамический прокси</returns>
    /// <exception cref="ArgumentNullException">Если httpClient равен null</exception>
    /// <exception cref="ArgumentException">Если TInterface не является интерфейсом</exception>
    public TInterface CreateClient<TInterface>(HttpClient httpClient, ILogger<HttpClientProxy>? logger = null) 
        where TInterface : class
    {
        if (httpClient == null)
            throw new ArgumentNullException(nameof(httpClient));

        ValidateAndCacheInterface<TInterface>();

        var proxy = DispatchProxy.Create<TInterface, HttpClientProxy>() as HttpClientProxy;
        if (proxy == null)
            throw new InvalidOperationException($"Failed to create proxy for interface {typeof(TInterface).Name}");

        proxy.Initialize(httpClient, _configuration.JsonOptions, logger);
        return (TInterface)(object)proxy;
    }

    /// <summary>
    /// Получает метаданные интерфейса для анализа
    /// </summary>
    /// <typeparam name="TInterface">Тип интерфейса</typeparam>
    /// <returns>Метаданные интерфейса</returns>
    public InterfaceMetadata GetInterfaceMetadata<TInterface>() where TInterface : class
    {
        ValidateAndCacheInterface<TInterface>();
        return _interfaceCache[typeof(TInterface)];
    }

    /// <summary>
    /// Проверяет и кеширует метаданные интерфейса
    /// </summary>
    /// <typeparam name="TInterface">Тип интерфейса для проверки</typeparam>
    /// <exception cref="ArgumentException">Если тип не является интерфейсом или не содержит HTTP методов</exception>
    private void ValidateAndCacheInterface<TInterface>() where TInterface : class
    {
        var interfaceType = typeof(TInterface);
        
        if (_interfaceCache.ContainsKey(interfaceType))
            return;

        if (!interfaceType.IsInterface)
            throw new ArgumentException($"{interfaceType.Name} must be an interface");

        var methods = interfaceType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        var hasHttpMethods = methods.Any(HasHttpAttributes);

        if (!hasHttpMethods)
            throw new ArgumentException($"Interface {interfaceType.Name} must contain methods with HTTP attributes (HttpGet, HttpPost, etc.)");

        var metadata = new InterfaceMetadata
        {
            Methods = methods,
            IsValidHttpInterface = true,
            InterfaceName = interfaceType.Name
        };

        _interfaceCache.TryAdd(interfaceType, metadata);
    }

    /// <summary>
    /// Проверяет, есть ли у метода HTTP атрибуты
    /// </summary>
    /// <param name="method">Метод для проверки</param>
    /// <returns>true, если у метода есть HTTP атрибуты</returns>
    private static bool HasHttpAttributes(MethodInfo method)
    {
        return method.GetCustomAttributes().Any(attr => 
            attr.GetType().Name.StartsWith("Http") && 
            attr.GetType().Name.EndsWith("Attribute"));
    }

    /// <summary>
    /// Генерирует имя HTTP клиента на основе имени интерфейса
    /// </summary>
    /// <typeparam name="TInterface">Тип интерфейса</typeparam>
    /// <returns>Имя клиента</returns>
    private static string GenerateClientName<TInterface>()
    {
        var name = typeof(TInterface).Name;
        // Убираем префикс 'I' если он есть
        return name.StartsWith('I') && name.Length > 1 ? name[1..] : name;
    }

    /// <summary>
    /// Настраивает базовые параметры HTTP клиента
    /// </summary>
    /// <param name="client">HTTP клиент для настройки</param>
    private void ConfigureHttpClient(HttpClient client)
    {
        client.BaseAddress = new Uri(_configuration.BaseUrl);
        client.Timeout = _configuration.Timeout;
        
        // Добавляем заголовки по умолчанию
        foreach (var header in _configuration.DefaultHeaders)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        }
    }

    /// <summary>
    /// Создает типизированный клиент для DI контейнера
    /// </summary>
    /// <typeparam name="TInterface">Тип интерфейса</typeparam>
    /// <param name="httpClient">HTTP клиент</param>
    /// <param name="serviceProvider">Провайдер сервисов DI</param>
    /// <returns>Экземпляр типизированного клиента</returns>
    private TInterface CreateTypedClient<TInterface>(HttpClient httpClient, IServiceProvider serviceProvider)
        where TInterface : class
    {
        var logger = serviceProvider.GetService<ILogger<HttpClientProxy>>();
        return CreateClient<TInterface>(httpClient, logger);
    }

    /// <summary>
    /// Регистрирует HTTP handlers в pipeline клиента
    /// </summary>
    /// <param name="builder">Builder HTTP клиента</param>
    private void RegisterHandlers(IHttpClientBuilder builder)
    {
        foreach (var handlerType in _configuration.HandlerTypes)
        {
            try
            {
                var method = typeof(HttpClientBuilderExtensions)
                    .GetMethod(nameof(HttpClientBuilderExtensions.AddHttpMessageHandler),
                        new[] { typeof(IHttpClientBuilder) })
                    ?.MakeGenericMethod(handlerType);

                if (method != null)
                {
                    method.Invoke(null, new object[] { builder });
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to register handler {handlerType.Name}", ex);
            }
        }
    }

    /// <summary>
    /// Возвращает информацию о текущей конфигурации
    /// </summary>
    /// <returns>Копия конфигурации для анализа</returns>
    public HttpClientConfigurationInfo GetConfigurationInfo()
    {
        return new HttpClientConfigurationInfo
        {
            BaseUrl = _configuration.BaseUrl,
            Timeout = _configuration.Timeout,
            HandlerCount = _configuration.HandlerTypes.Count,
            DefaultHeadersCount = _configuration.DefaultHeaders.Count,
            HasRetryPolicy = _configuration.RetryOptions != null
        };
    }
}

/// <summary>
/// Информация о конфигурации HTTP клиента для анализа
/// </summary>
public class HttpClientConfigurationInfo
{
    public string BaseUrl { get; init; } = string.Empty;
    public TimeSpan Timeout { get; init; }
    public int HandlerCount { get; init; }
    public int DefaultHeadersCount { get; init; }
    public bool HasRetryPolicy { get; init; }
}
