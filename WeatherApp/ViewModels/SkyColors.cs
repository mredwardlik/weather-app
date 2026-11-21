using WeatherApp.Core;

namespace WeatherApp.ViewModels;

/// <summary>Background gradient for the weather and the time of day.</summary>
public sealed record SkyColors(Color Top, Color Bottom)
{
    /// <summary>The color of the first version.</summary>
    public static SkyColors Default { get; } = new(Color.FromArgb("#7585BA"), Color.FromArgb("#9AA6CF"));

    public static SkyColors For(Sky sky, bool isDay) => (sky, isDay) switch
    {
        (Sky.Clear, true) => Make("#3F7FD1", "#86B9EE"),
        (Sky.Clear, false) => Make("#141B33", "#34416E"),
        (Sky.PartlyCloudy, true) => Make("#5B7BC0", "#9DB5E2"),
        (Sky.PartlyCloudy, false) => Make("#1E2643", "#424F7C"),
        (Sky.Cloudy, true) => Default,
        (Sky.Cloudy, false) => Make("#272D45", "#4B5376"),
        (Sky.Fog, true) => Make("#7F899E", "#B1B8C6"),
        (Sky.Fog, false) => Make("#2F3442", "#585E70"),
        (Sky.Drizzle or Sky.Rain, true) => Make("#475779", "#7383A6"),
        (Sky.Drizzle or Sky.Rain, false) => Make("#1A2131", "#3A4560"),
        (Sky.Snow, true) => Make("#7F96BF", "#C3D2EA"),
        (Sky.Snow, false) => Make("#2D3852", "#5A6A8C"),
        (Sky.Thunderstorm, true) => Make("#373B57", "#5B5F82"),
        (Sky.Thunderstorm, false) => Make("#16182A", "#33374F"),
        _ => Default,
    };

    private static SkyColors Make(string top, string bottom) => new(Color.FromArgb(top), Color.FromArgb(bottom));
}
