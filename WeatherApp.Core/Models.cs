namespace WeatherApp.Core;

/// <summary>A city or any point on the map.</summary>
public sealed record Place(string Name, double Latitude, double Longitude, string? Region = null, string? Country = null)
{
    public static Place Almaty { get; } = new("Almaty", 43.2525, 76.9115, "Almaty", "Kazakhstan");

    /// <summary>"Almaty, Kazakhstan", or the name alone.</summary>
    public string Title => string.IsNullOrWhiteSpace(Country) ? Name : $"{Name}, {Country}";

    /// <summary>Region and country for a list of search results: "Karaganda, Kazakhstan".</summary>
    public string Details => string.Join(", ", new[] { Region, Country }
        .Where(part => !string.IsNullOrWhiteSpace(part) && part != Name));
}

public sealed record CurrentWeather(
    DateTime Time,
    double Temperature,
    double FeelsLike,
    int Humidity,
    double WindSpeed,
    double Pressure,
    int CloudCover,
    int WeatherCode,
    bool IsDay)
{
    public WeatherCondition Condition => WeatherCodes.Describe(WeatherCode);
}

public sealed record DailyForecast(DateOnly Date, double MaxTemperature, double MinTemperature, int WeatherCode)
{
    public WeatherCondition Condition => WeatherCodes.Describe(WeatherCode);
}

public sealed record Forecast(Place Place, CurrentWeather Current, IReadOnlyList<DailyForecast> Days, string TimeZone)
{
    /// <summary>Days after today, as many as asked.</summary>
    public IReadOnlyList<DailyForecast> NextDays(int count) =>
        Days.Where(day => day.Date > DateOnly.FromDateTime(Current.Time)).Take(count).ToList();
}
