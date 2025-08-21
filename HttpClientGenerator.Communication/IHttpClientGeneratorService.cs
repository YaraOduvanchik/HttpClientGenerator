using HttpClientGenerator.Communication.Controllers;

namespace HttpClientGenerator.Communication;

public interface IHttpClientGeneratorService
{
    IWeatherForecastController WeatherForecastController { get; }
}
