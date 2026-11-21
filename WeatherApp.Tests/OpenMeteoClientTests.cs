using System.Globalization;
using System.Net;
using WeatherApp.Core;
using Xunit;

namespace WeatherApp.Tests;

/// <summary>Answers requests with saved responses of Open-Meteo and records the URLs.</summary>
internal sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<Uri> Requests { get; } = [];

    public static HttpResponseMessage Fixture(string name, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(File.ReadAllText(Path.Combine("Fixtures", name))) };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!);
        return Task.FromResult(respond(request));
    }
}

public class OpenMeteoClientTests
{
    private static (OpenMeteoClient Client, FakeHandler Handler) Make(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new FakeHandler(respond);
        return (new OpenMeteoClient(new HttpClient(handler)), handler);
    }

    [Fact]
    public async Task Forecast_is_parsed()
    {
        var (client, _) = Make(_ => FakeHandler.Fixture("forecast.json"));
        var forecast = await client.GetForecastAsync(Place.Almaty);

        Assert.Equal("Asia/Almaty", forecast.TimeZone);
        Assert.Equal(new DateTime(2026, 11, 21, 23, 30, 0), forecast.Current.Time);
        Assert.Equal(7.8, forecast.Current.Temperature);
        Assert.Equal(5.5, forecast.Current.FeelsLike);
        Assert.Equal(67, forecast.Current.Humidity);
        Assert.Equal(0.88, forecast.Current.WindSpeed);
        Assert.Equal(1021.2, forecast.Current.Pressure);
        Assert.Equal(100, forecast.Current.CloudCover);
        Assert.False(forecast.Current.IsDay);
        Assert.Equal("Fog", forecast.Current.Condition.Description);

        Assert.Equal(5, forecast.Days.Count);
        Assert.Equal(new DateOnly(2026, 11, 21), forecast.Days[0].Date);
        Assert.Equal(14.0, forecast.Days[0].MaxTemperature);
    }

    [Fact]
    public async Task Next_days_start_tomorrow()
    {
        var (client, _) = Make(_ => FakeHandler.Fixture("forecast.json"));
        var forecast = await client.GetForecastAsync(Place.Almaty);

        var next = forecast.NextDays(4);
        Assert.Equal(4, next.Count);
        Assert.Equal(new DateOnly(2026, 11, 22), next[0].Date);
        Assert.Equal(new DateOnly(2026, 11, 25), next[3].Date);
    }

    [Fact]
    public async Task Forecast_url_uses_invariant_numbers()
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("ru-RU");  // decimal comma
        try
        {
            var (client, handler) = Make(_ => FakeHandler.Fixture("forecast.json"));
            await client.GetForecastAsync(new Place("Somewhere", 43.2525, -76.9115));
            var url = handler.Requests.Single().ToString();
            Assert.Contains("latitude=43.2525&longitude=-76.9115", url);
            Assert.Contains("wind_speed_unit=ms", url);
            Assert.Contains("timezone=auto", url);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public async Task Search_returns_places()
    {
        var (client, handler) = Make(_ => FakeHandler.Fixture("search.json"));
        var places = await client.SearchAsync("  Almaty ");

        Assert.Contains("name=Almaty&", handler.Requests.Single().ToString());
        Assert.Equal("Almaty", places[0].Name);
        Assert.Equal("Almaty, Kazakhstan", places[0].Title);
        Assert.Equal("Kazakhstan", places[0].Details);  // region equal to the name is skipped
        Assert.Equal("Karaganda, Kazakhstan", places[2].Details);
        Assert.Equal(43.25249, places[0].Latitude);
    }

    [Fact]
    public async Task Search_escapes_the_query()
    {
        var (client, handler) = Make(_ => FakeHandler.Fixture("search-empty.json"));
        await client.SearchAsync("Нур-Султан & co");
        Assert.Contains("name=%D0%9D%D1%83%D1%80-%D0%A1%D1%83%D0%BB%D1%82%D0%B0%D0%BD%20%26%20co&",
            handler.Requests.Single().AbsoluteUri);
    }

    [Theory]
    [InlineData("Almaty", "en")]
    [InlineData("München", "en")]
    [InlineData("Алматы", "ru")]
    [InlineData("Өскемен", "ru")]
    public void Search_language_follows_the_alphabet(string query, string language)
    {
        Assert.Equal(language, OpenMeteoClient.SearchLanguage(query));
    }

    [Fact]
    public async Task Cyrillic_query_is_searched_in_russian()
    {
        var (client, handler) = Make(_ => FakeHandler.Fixture("search-empty.json"));
        await client.SearchAsync("Алматы");
        Assert.Contains("&language=ru&", handler.Requests.Single().AbsoluteUri);
    }

    [Fact]
    public async Task Search_without_results_is_empty()
    {
        var (client, _) = Make(_ => FakeHandler.Fixture("search-empty.json"));
        Assert.Empty(await client.SearchAsync("qwertyzx"));
    }

    [Fact]
    public async Task Short_query_doesnt_call_the_service()
    {
        var (client, handler) = Make(_ => throw new InvalidOperationException());
        Assert.Empty(await client.SearchAsync(" a "));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Reason_of_a_bad_request_is_shown()
    {
        var (client, _) = Make(_ => FakeHandler.Fixture("error.json", HttpStatusCode.BadRequest));
        var error = await Assert.ThrowsAsync<WeatherException>(() => client.GetForecastAsync(new Place("x", 999, 0)));
        Assert.StartsWith("Latitude must be in range", error.Message);
    }

    [Fact]
    public async Task Server_error()
    {
        var (client, _) = Make(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var error = await Assert.ThrowsAsync<WeatherException>(() => client.GetForecastAsync(Place.Almaty));
        Assert.Contains("unavailable (503)", error.Message);
    }

    [Fact]
    public async Task No_connection()
    {
        var (client, _) = Make(_ => throw new HttpRequestException("No such host is known."));
        var error = await Assert.ThrowsAsync<WeatherException>(() => client.SearchAsync("Almaty"));
        Assert.Contains("Check the internet", error.Message);
    }

    [Fact]
    public async Task Broken_json()
    {
        var (client, _) = Make(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>") });
        var error = await Assert.ThrowsAsync<WeatherException>(() => client.GetForecastAsync(Place.Almaty));
        Assert.Contains("can't be read", error.Message);
    }

    [Fact]
    public async Task Incomplete_answer()
    {
        var (client, _) = Make(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        var error = await Assert.ThrowsAsync<WeatherException>(() => client.GetForecastAsync(Place.Almaty));
        Assert.Contains("incomplete", error.Message);
    }

    [Fact]
    public async Task Cancellation_is_not_an_error()
    {
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        var (client, _) = Make(_ => throw new TaskCanceledException());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetForecastAsync(Place.Almaty, cancel.Token));
    }
}

public class WeatherCodesTests
{
    [Theory]
    [InlineData(0, true, "Clear sky", "w01d.png")]
    [InlineData(0, false, "Clear sky", "w01n.png")]
    [InlineData(2, true, "Partly cloudy", "w03d.png")]
    [InlineData(45, false, "Fog", "w50n.png")]
    [InlineData(63, true, "Rain", "w10d.png")]
    [InlineData(81, true, "Showers", "w09d.png")]
    [InlineData(75, true, "Heavy snow", "w13d.png")]
    [InlineData(95, false, "Thunderstorm", "w11n.png")]
    public void Codes_have_descriptions_and_icons(int code, bool isDay, string description, string icon)
    {
        var condition = WeatherCodes.Describe(code);
        Assert.Equal(description, condition.Description);
        Assert.Equal(icon, condition.IconFile(isDay));
    }

    [Fact]
    public void Unknown_code_still_has_an_icon()
    {
        var condition = WeatherCodes.Describe(42);
        Assert.Equal("Unknown", condition.Description);
        Assert.Equal("w03d.png", condition.IconFile(true));
    }

    [Fact]
    public void Every_icon_exists_in_the_app()
    {
        var images = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "WeatherApp", "Resources", "Images");
        foreach (var code in WeatherCodes.Known)
        foreach (var isDay in new[] { true, false })
        {
            var file = WeatherCodes.Describe(code).IconFile(isDay);
            Assert.True(File.Exists(Path.Combine(images, file)), $"{file} for code {code} is missing");
        }
    }
}
