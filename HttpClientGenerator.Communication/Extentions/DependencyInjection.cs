using HttpClientGenerator.Communication.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Shared;

namespace HttpClientGenerator.Communication.Extentions;

public static class DependencyInjection
{
    public static IServiceCollection AddHttpGenerator(
        this IServiceCollection services,
        string baseUrl)
    {
        services.AddHttpClientGenerator(baseUrl, builder =>
        {
            builder.WithTimeout(TimeSpan.FromSeconds(30));
        });

        var serviceProvider = services.BuildServiceProvider();
        var generator = serviceProvider.GetRequiredService<Shared.HttpClientGenerator>();

        generator.Register<IWeatherForecastController>(services);

        services.AddScoped<IHttpClientGeneratorService, HttpClientGeneratorService>();

        return services;
    }
}
