using HttpClientGenerator.Communication.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Shared;

namespace HttpClientGenerator.Communication.Extentions;

public static class DependencyInjection
{
    public static IServiceCollection AddHttpGenerator(
        this IServiceCollection services,
        string baseUrl,
        Action<HttpClientGeneratorBuilder>? configureCustomHandlers = null)
    {
        // Регистрируем кастомные handlers специфичные для Communication проекта
        services.AddScoped<AuthenticationHandler>();
        services.AddScoped<CorrelationIdHandler>();

        services.AddHttpClientGenerator(baseUrl, builder =>
        {
            builder.WithTimeout(TimeSpan.FromSeconds(30));

            // Добавляем кастомные handlers специфичные для Communication проекта
            builder.AddHandler<AuthenticationHandler>();
            builder.AddHandler<CorrelationIdHandler>();

            // Применяем дополнительную конфигурацию от вызывающего кода
            configureCustomHandlers?.Invoke(builder);
        });

        var serviceProvider = services.BuildServiceProvider();
        var generator = serviceProvider.GetRequiredService<Shared.HttpClientGenerator>();

        generator.Register<IWeatherForecastHttpClient>(services);

        return services;
    }
}
