using HttpClientGenerator.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace HttpClientGenerator.Communication.Controllers;

public interface IWeatherForecastController
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