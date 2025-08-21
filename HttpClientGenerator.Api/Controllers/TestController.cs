using HttpClientGenerator.Communication;
using Microsoft.AspNetCore.Mvc;

namespace HttpClientGenerator.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class TestController : ControllerBase
{
    private readonly IHttpClientGeneratorService _httpClientGeneratorService;

    public TestController(IHttpClientGeneratorService httpClientGeneratorService)
    {
        _httpClientGeneratorService = httpClientGeneratorService;
    }

    [HttpGet]
    public async Task<IActionResult> TestGet()
    {
        var result = await _httpClientGeneratorService.WeatherForecastController.GetWeatherForecastAsync();
        return Ok(result);
    }
}
