using HttpClientGenerator.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace HttpClientGenerator.Communication;

public interface IWeatherForecastHttpClient
{
    [HttpGet("weatherforecast")]
    Task<IEnumerable<WeatherForecast>> GetWeatherForecastAsync();
    
    [HttpGet("api/weather")]
    Task<IEnumerable<WeatherForecast>> GetWeatherAsync();

    [HttpPost("api/weather")]
    Task<WeatherForecast> CreateWeatherAsync([FromBody] WeatherForecast weather);
    
    [HttpGet("api/weather/{days}")] 
    Task<IEnumerable<WeatherForecast>> GetWeatherForDaysAsync(int days);
}