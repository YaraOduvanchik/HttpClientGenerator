using HttpClientGenerator.Communication;
using HttpClientGenerator.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace HttpClientGenerator.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class MinimalApiTestController : ControllerBase
{
    private readonly IHttpClientGeneratorService _httpClientGeneratorService;


    public MinimalApiTestController(IHttpClientGeneratorService httpClientGeneratorService)
    {
        _httpClientGeneratorService = httpClientGeneratorService;
    }
    
    [HttpGet("weather")]
    public async Task<IActionResult> GetWeatherFromMinimalApi()
    {
        var result = await _httpClientGeneratorService.WeatherForecastController.GetWeatherAsync();
        return Ok(new { Source = "Minimal API", Data = result });
    }
    
    [HttpPost("weather")]
    public async Task<IActionResult> CreateWeatherInMinimalApi([FromBody] WeatherForecast weather)
    {
        var result = await _httpClientGeneratorService.WeatherForecastController.CreateWeatherAsync(weather);
        return Ok(new { Source = "Minimal API", Created = result });
    }
    
    [HttpGet("weather/{days:int}")]
    public async Task<IActionResult> GetWeatherForDaysFromMinimalApi(int days)
    {
        var result = await _httpClientGeneratorService.WeatherForecastController.GetWeatherForDaysAsync(days);
        return Ok(new { Source = "Minimal API with parameter", Days = days, Data = result });
    }
    
    [HttpGet("demo")]
    public async Task<IActionResult> DemoAllMinimalApiMethods()
    {
        try
        {
            var newWeather = new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(10)),
                TemperatureC = 25,
                Summary = "Sunny"
            };
            
            var weatherDefault = await _httpClientGeneratorService.WeatherForecastController.GetWeatherAsync();
            var weatherForWeek = await _httpClientGeneratorService.WeatherForecastController.GetWeatherForDaysAsync(7);
            var createdWeather = await _httpClientGeneratorService.WeatherForecastController.CreateWeatherAsync(newWeather);

            return Ok(new
            {
                Message = "Все методы Minimal API протестированы успешно!",
                Results = new
                {
                    DefaultWeather = new { Method = "GET /api/weather", Count = weatherDefault.Count(), Data = weatherDefault },
                    WeekWeather = new { Method = "GET /api/weather/7", Count = weatherForWeek.Count(), Data = weatherForWeek },
                    CreatedWeather = new { Method = "POST /api/weather", Data = createdWeather }
                },
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message, StackTrace = ex.StackTrace });
        }
    }
}
