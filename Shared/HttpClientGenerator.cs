using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Shared;

/// <summary>
/// Генератор HTTP клиентов на основе интерфейсов с HTTP атрибутами.
/// Автоматически создает реализации интерфейсов, анализируя методы с атрибутами HttpGet, HttpPost и т.д.
/// </summary>
public class HttpClientGenerator
{
    private readonly HttpClientConfiguration _configuration;

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
    public static HttpClientGeneratorBuilder BuildForUrl(string baseUrl) => new(baseUrl);

    /// <summary>
    /// Регистрирует типизированный HTTP клиент для указанного интерфейса в DI контейнере
    /// </summary>
    /// <typeparam name="TInterface">Тип интерфейса с HTTP атрибутами</typeparam>
    /// <param name="services">Коллекция сервисов DI</param>
    /// <param name="clientName">Имя HTTP клиента (по умолчанию - имя интерфейса)</param>
    /// <returns>Builder для дополнительной настройки HTTP клиента</returns>
    /// <exception cref="ArgumentException">Если TInterface не является интерфейсом</exception>
    public IHttpClientBuilder Register<TInterface>(IServiceCollection services, string? clientName = null)
        where TInterface : class
    {
        ValidateInterface<TInterface>();

        var name = clientName ?? GenerateClientName<TInterface>();

        var httpClientBuilder = services
            .AddHttpClient(name, ConfigureHttpClient)
            .AddTypedClient<TInterface>(CreateTypedClient<TInterface>);

        RegisterHandlers(httpClientBuilder);

        return httpClientBuilder;
    }

    /// <summary>
    /// Создает экземпляр HTTP клиента для указанного интерфейса
    /// </summary>
    /// <typeparam name="TInterface">Тип интерфейса с HTTP атрибутами</typeparam>
    /// <param name="httpClient">Настроенный HttpClient</param>
    /// <returns>Реализация интерфейса через динамический прокси</returns>
    public TInterface CreateClient<TInterface>(HttpClient httpClient) where TInterface : class
    {
        var proxy = DispatchProxy.Create<TInterface, HttpClientProxy>() as HttpClientProxy;
        proxy?.Initialize(httpClient, _configuration.JsonOptions);
        return (TInterface)(object)proxy!;
    }

    /// <summary>
    /// Проверяет, что указанный тип является интерфейсом
    /// </summary>
    /// <typeparam name="TInterface">Тип для проверки</typeparam>
    /// <exception cref="ArgumentException">Если тип не является интерфейсом</exception>
    private static void ValidateInterface<TInterface>() where TInterface : class
    {
        if (!typeof(TInterface).IsInterface)
            throw new ArgumentException($"{typeof(TInterface).Name} must be an interface");
    }

    /// <summary>
    /// Генерирует имя HTTP клиента на основе имени интерфейса
    /// </summary>
    /// <typeparam name="TInterface">Тип интерфейса</typeparam>
    /// <returns>Имя клиента</returns>
    private static string GenerateClientName<TInterface>() => typeof(TInterface).Name;

    /// <summary>
    /// Настраивает базовые параметры HTTP клиента
    /// </summary>
    /// <param name="client">HTTP клиент для настройки</param>
    private void ConfigureHttpClient(HttpClient client)
    {
        client.BaseAddress = new Uri(_configuration.BaseUrl);
        client.Timeout = _configuration.Timeout;
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
        return CreateClient<TInterface>(httpClient);
    }

    /// <summary>
    /// Регистрирует HTTP handlers в pipeline клиента
    /// </summary>
    /// <param name="builder">Builder HTTP клиента</param>
    private void RegisterHandlers(IHttpClientBuilder builder)
    {
        foreach (var handlerType in _configuration.HandlerTypes)
        {
            var method = typeof(HttpClientBuilderExtensions)
                .GetMethod(nameof(HttpClientBuilderExtensions.AddHttpMessageHandler),
                    new[] { typeof(IHttpClientBuilder) })!
                .MakeGenericMethod(handlerType);
            method.Invoke(null, new object[] { builder });
        }
    }
}
