using JsonSchemaMapper;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

var forecast = new WeatherForecast
{
    LocationName = "London, UK",
    Date = new DateOnly(2024, 03, 26),
    Summary = "Sunny spells with light rain spreading later",
    HourlyForecasts =
    [
        new()
        {
            Time = new TimeOnly(9, 0),
            Type = WeatherType.Sunny,
            TemperatureC = 9
        },
        new()
        {
            Time = new TimeOnly(12, 0),
            Type = WeatherType.Cloudy,
            TemperatureC = 8
        },
        new()
        {
            Time = new TimeOnly(15, 0),
            Type = WeatherType.Rainy,
            TemperatureC = 6
        },
    ]
};

JsonNode schema = MyContext.Default.WeatherForecast.GetJsonSchema();
Console.WriteLine(schema.ToString());

MethodInfo method = typeof(MyKernelPlugin).GetMethod(nameof(MyKernelPlugin.ShouldStepOutside))!;
schema = MyContext.Default.Options.GetJsonSchema(method);
Console.WriteLine(schema.ToString());

record WeatherForecast
{
    public required string LocationName { get; init; }
    public required DateOnly Date { get; init; }
    public string? Summary { get; init; }
    public HourlyForecast[]? HourlyForecasts { get; init; }
}

record HourlyForecast
{
    public required TimeOnly Time { get; init; }
    public required WeatherType Type { get; init; }
    public required int TemperatureC { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<WeatherType>))]
enum WeatherType
{
    Sunny, Cloudy, Rainy
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(WeatherForecast))]
partial class MyContext : JsonSerializerContext;

static class MyKernelPlugin
{
    //[KernelFunction]
    [Description("An advanced algorithm that decides if I should go outside for a walk.")]
    public static bool ShouldStepOutside(WeatherForecast forecast, int determinationLevel = 3)
    {
        return forecast.HourlyForecasts?.All(h => h.Type is not WeatherType.Rainy || h.TemperatureC < 7 ) is true
            || determinationLevel > 10;
    }
}