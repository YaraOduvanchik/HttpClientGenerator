using HttpClientGenerator.Communication;
using Microsoft.AspNetCore.Mvc;

namespace HttpClientGenerator.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class TestController : ControllerBase
{
    private readonly IWeatherForecastHttpClient _weatherForecastHttpClient;

    public TestController(IWeatherForecastHttpClient weatherForecastHttpClient)
    {
        _weatherForecastHttpClient = weatherForecastHttpClient;
    }

    [HttpGet]
    public async Task<IActionResult> TestGet()
    {
        var result = await _weatherForecastHttpClient.GetWeatherForecastAsync();
        return Ok(result);
    }
}