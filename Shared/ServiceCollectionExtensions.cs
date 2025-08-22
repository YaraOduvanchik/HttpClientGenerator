using Microsoft.Extensions.DependencyInjection;
using Shared.Handlers;

namespace Shared;

/// <summary>
/// Extension методы для регистрации HTTP клиентов и handlers в DI контейнере
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует все необходимые HTTP handlers в DI контейнере
    /// </summary>
    /// <param name="services">Коллекция сервисов DI</param>
    /// <returns>Коллекция сервисов для цепочки вызовов</returns>
    public static IServiceCollection AddHttpClientHandlers(this IServiceCollection services)
    {
        services.AddScoped<ExceptionHandler>();
        services.AddScoped<LoggingHandler>();

        // Здесь можно добавить регистрацию других handlers
        // services.AddScoped<CorrelationIdHandler>();
        // services.AddScoped<RetryHandler>();

        return services;
    }

    /// <summary>
    /// Регистрирует HTTP генератор с автоматической регистрацией handlers
    /// </summary>
    /// <param name="services">Коллекция сервисов DI</param>
    /// <param name="baseUrl">Базовый URL для HTTP клиентов</param>
    /// <param name="configure">Дополнительная конфигурация генератора</param>
    /// <returns>Коллекция сервисов для цепочки вызовов</returns>
    public static IServiceCollection AddHttpClientGenerator(
        this IServiceCollection services,
        string baseUrl,
        Action<HttpClientGeneratorBuilder>? configure = null)
    {
        services.AddHttpClientHandlers();

        var builder = HttpClientGenerator.BuildForUrl(baseUrl)
            .WithDefaultJsonOptions()
            .WithLogging();

        configure?.Invoke(builder);

        var generator = builder.Create();

        services.AddSingleton(generator);

        return services;
    }
}
