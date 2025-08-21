using HttpClientGenerator.Communication.Extentions;
using HttpClientGenerator.Contracts;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Регистрируем HTTP генератор для тестирования (сам с собой)
builder.Services.AddHttpGenerator("http://localhost:5257");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// Minimal API endpoints
app.MapGet("/api/weather", () =>
{
    var summaries = new[] { "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching" };

    var forecast = Enumerable.Range(1, 5).Select(index => new WeatherForecast
    {
        Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
        TemperatureC = Random.Shared.Next(-20, 55),
        Summary = summaries[Random.Shared.Next(summaries.Length)]
    }).ToArray();

    return Results.Ok(forecast);
})
.WithName("GetWeatherMinimal") ;

app.MapPost("/api/weather", (WeatherForecast weather) =>
{
    // Простая обработка POST запроса
    return Results.Created($"/api/weather/{weather.Date}", weather);
})
.WithName("CreateWeatherMinimal") ;

// Дополнительный endpoint для демонстрации различных возможностей
app.MapGet("/api/weather/{days:int}", (int days) =>
{
    var summaries = new[] { "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching" };

    var forecast = Enumerable.Range(1, days).Select(index => new WeatherForecast
    {
        Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
        TemperatureC = Random.Shared.Next(-20, 55),
        Summary = summaries[Random.Shared.Next(summaries.Length)]
    }).ToArray();

    return Results.Ok(forecast);
})
.WithName("GetWeatherForDays") ;

app.Run();
