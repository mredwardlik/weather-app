namespace WeatherApp.Core;

public enum Sky
{
    Clear,
    PartlyCloudy,
    Cloudy,
    Fog,
    Drizzle,
    Rain,
    Snow,
    Thunderstorm,
}

/// <summary>What a WMO weather code means and how to show it.</summary>
/// <param name="Icon">Number of the icon: w01d.png … w50n.png, the same set as OpenWeatherMap.</param>
public sealed record WeatherCondition(int Code, string Description, Sky Sky, string Icon)
{
    public string IconFile(bool isDay) => $"w{Icon}{(isDay ? 'd' : 'n')}.png";
}

/// <summary>
/// WMO weather interpretation codes used by Open-Meteo,
/// see https://open-meteo.com/en/docs#weather_variable_documentation.
/// </summary>
public static class WeatherCodes
{
    private static readonly Dictionary<int, (string Description, Sky Sky, string Icon)> Codes = new()
    {
        [0] = ("Clear sky", Sky.Clear, "01"),
        [1] = ("Mainly clear", Sky.PartlyCloudy, "02"),
        [2] = ("Partly cloudy", Sky.PartlyCloudy, "03"),
        [3] = ("Overcast", Sky.Cloudy, "04"),
        [45] = ("Fog", Sky.Fog, "50"),
        [48] = ("Rime fog", Sky.Fog, "50"),
        [51] = ("Light drizzle", Sky.Drizzle, "09"),
        [53] = ("Drizzle", Sky.Drizzle, "09"),
        [55] = ("Dense drizzle", Sky.Drizzle, "09"),
        [56] = ("Freezing drizzle", Sky.Drizzle, "09"),
        [57] = ("Dense freezing drizzle", Sky.Drizzle, "09"),
        [61] = ("Light rain", Sky.Rain, "10"),
        [63] = ("Rain", Sky.Rain, "10"),
        [65] = ("Heavy rain", Sky.Rain, "10"),
        [66] = ("Freezing rain", Sky.Rain, "10"),
        [67] = ("Heavy freezing rain", Sky.Rain, "10"),
        [71] = ("Light snow", Sky.Snow, "13"),
        [73] = ("Snow", Sky.Snow, "13"),
        [75] = ("Heavy snow", Sky.Snow, "13"),
        [77] = ("Snow grains", Sky.Snow, "13"),
        [80] = ("Light showers", Sky.Rain, "09"),
        [81] = ("Showers", Sky.Rain, "09"),
        [82] = ("Violent showers", Sky.Rain, "09"),
        [85] = ("Snow showers", Sky.Snow, "13"),
        [86] = ("Heavy snow showers", Sky.Snow, "13"),
        [95] = ("Thunderstorm", Sky.Thunderstorm, "11"),
        [96] = ("Thunderstorm with hail", Sky.Thunderstorm, "11"),
        [99] = ("Thunderstorm with heavy hail", Sky.Thunderstorm, "11"),
    };

    public static IReadOnlyCollection<int> Known => Codes.Keys;

    public static WeatherCondition Describe(int code) =>
        Codes.TryGetValue(code, out var info)
            ? new WeatherCondition(code, info.Description, info.Sky, info.Icon)
            : new WeatherCondition(code, "Unknown", Sky.Cloudy, "03");
}
