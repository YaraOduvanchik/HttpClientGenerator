using HttpClientGenerator.Communication.Controllers;

namespace HttpClientGenerator.Communication;

public class HttpClientGeneratorService : IHttpClientGeneratorService
{
    public HttpClientGeneratorService(IWeatherForecastController weatherForecastController)
    {
        WeatherForecastController = weatherForecastController;
    }
    
    public IWeatherForecastController WeatherForecastController { get; }
}
