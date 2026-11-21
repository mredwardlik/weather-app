using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WeatherApp.Core;

/// <summary>The weather can't be loaded. <see cref="Exception.Message"/> is ready to show.</summary>
public sealed class WeatherException(string message, Exception? inner = null) : Exception(message, inner);

public interface IWeatherClient
{
    Task<Forecast> GetForecastAsync(Place place, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Place>> SearchAsync(string query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Weather and city search from Open-Meteo (https://open-meteo.com). Free, no API key.
/// </summary>
public sealed class OpenMeteoClient(HttpClient http) : IWeatherClient
{
    public const string ForecastUrl = "https://api.open-meteo.com/v1/forecast";
    public const string SearchUrl = "https://geocoding-api.open-meteo.com/v1/search";
    public const int ForecastDays = 5;

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public async Task<Forecast> GetForecastAsync(Place place, CancellationToken cancellationToken = default)
    {
        var url = $"{ForecastUrl}?latitude={Number(place.Latitude)}&longitude={Number(place.Longitude)}"
                  + "&current=temperature_2m,relative_humidity_2m,apparent_temperature,is_day,weather_code,"
                  + "cloud_cover,pressure_msl,wind_speed_10m"
                  + "&daily=weather_code,temperature_2m_max,temperature_2m_min"
                  + $"&timezone=auto&forecast_days={ForecastDays}&wind_speed_unit=ms";

        var data = await GetAsync<ForecastDto>(url, cancellationToken);
        if (data.Current is null || data.Daily is null)
            throw new WeatherException("The weather service sent an incomplete answer.");

        var current = data.Current;
        var daily = data.Daily;
        var days = Enumerable.Range(0, daily.Time.Length)
            .Where(i => daily.WeatherCode[i] is not null && daily.Max[i] is not null && daily.Min[i] is not null)
            .Select(i => new DailyForecast(DateOnly.Parse(daily.Time[i], CultureInfo.InvariantCulture),
                daily.Max[i]!.Value, daily.Min[i]!.Value, daily.WeatherCode[i]!.Value))
            .ToList();

        return new Forecast(
            place,
            new CurrentWeather(
                DateTime.Parse(current.Time, CultureInfo.InvariantCulture),
                current.Temperature, current.FeelsLike, current.Humidity, current.WindSpeed,
                current.Pressure, current.CloudCover, current.WeatherCode, current.IsDay == 1),
            days,
            data.Timezone ?? "GMT");
    }

    public async Task<IReadOnlyList<Place>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        query = query.Trim();
        if (query.Length < 2)
            return [];

        var url = $"{SearchUrl}?name={Uri.EscapeDataString(query)}&count=10&language={SearchLanguage(query)}&format=json";
        var data = await GetAsync<SearchDto>(url, cancellationToken);
        return (data.Results ?? [])
            .Select(r => new Place(r.Name, r.Latitude, r.Longitude, r.Admin1, r.Country))
            .ToList();
    }

    /// <summary>Open-Meteo finds names only in the language of the request,
    /// so "Алматы" needs Russian and "Almaty" English.</summary>
    public static string SearchLanguage(string query) =>
        query.Any(c => c is >= '\u0400' and <= '\u04FF') ? "ru" : "en";

    private async Task<T> GetAsync<T>(string url, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync(url, cancellationToken);
        }
        catch (HttpRequestException error)
        {
            throw new WeatherException("No connection to the weather service. Check the internet.", error);
        }
        catch (TaskCanceledException error) when (!cancellationToken.IsCancellationRequested)
        {
            throw new WeatherException("The weather service doesn't answer. Try again later.", error);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                var problem = await TryRead<ErrorDto>(response, cancellationToken);
                throw new WeatherException(problem?.Reason ?? "The weather service didn't accept the request.");
            }
            if (!response.IsSuccessStatusCode)
                throw new WeatherException($"The weather service is unavailable ({(int)response.StatusCode}). Try again later.");

            return await TryRead<T>(response, cancellationToken)
                   ?? throw new WeatherException("The weather service sent an answer that can't be read.");
        }
    }

    private static async Task<T?> TryRead<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static string Number(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private sealed record ErrorDto(string? Reason);

    private sealed record ForecastDto(string? Timezone, CurrentDto? Current, DailyDto? Daily);

    private sealed record CurrentDto(
        string Time,
        [property: JsonPropertyName("temperature_2m")] double Temperature,
        [property: JsonPropertyName("apparent_temperature")] double FeelsLike,
        [property: JsonPropertyName("relative_humidity_2m")] int Humidity,
        [property: JsonPropertyName("wind_speed_10m")] double WindSpeed,
        [property: JsonPropertyName("pressure_msl")] double Pressure,
        [property: JsonPropertyName("cloud_cover")] int CloudCover,
        [property: JsonPropertyName("weather_code")] int WeatherCode,
        [property: JsonPropertyName("is_day")] int IsDay);

    private sealed record DailyDto(
        string[] Time,
        [property: JsonPropertyName("weather_code")] int?[] WeatherCode,
        [property: JsonPropertyName("temperature_2m_max")] double?[] Max,
        [property: JsonPropertyName("temperature_2m_min")] double?[] Min);

    private sealed record SearchDto(ResultDto[]? Results);

    private sealed record ResultDto(string Name, double Latitude, double Longitude, string? Country, string? Admin1);
}
