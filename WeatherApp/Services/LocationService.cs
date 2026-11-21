using System.Diagnostics;
using System.Globalization;
using WeatherApp.Core;

namespace WeatherApp.Services;

public interface ILocationService
{
    /// <summary>The place of the device, or null if the location is off, not allowed or too slow.</summary>
    Task<Place?> GetCurrentPlaceAsync();
}

public sealed class DeviceLocationService(IGeolocation geolocation, IGeocoding geocoding) : ILocationService
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public async Task<Place?> GetCurrentPlaceAsync()
    {
        var find = FindAsync();
        var finished = await Task.WhenAny(find, Task.Delay(Timeout));
        if (finished != find)
        {
            Debug.WriteLine("Location: no answer in time");
            return null;
        }
        return await find;
    }

    private async Task<Place?> FindAsync()
    {
        try
        {
            var status = await MainThread.InvokeOnMainThreadAsync(Permissions.RequestAsync<Permissions.LocationWhenInUse>);
            if (status != PermissionStatus.Granted)
                return null;

            var location = await geolocation.GetLastKnownLocationAsync()
                           ?? await geolocation.GetLocationAsync(
                               new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)));
            if (location is null)
                return null;

            var placemark = await FindPlacemarkAsync(location);
            var name = placemark?.Locality ?? placemark?.SubAdminArea ?? placemark?.AdminArea
                       ?? Coordinates(location.Latitude, location.Longitude);
            return new Place(name, location.Latitude, location.Longitude, placemark?.AdminArea, placemark?.CountryName);
        }
        catch (Exception error)
        {
            // Location is optional: turned off, not allowed or not supported means "search for a city".
            Debug.WriteLine($"Location: {error.GetType().Name}: {error.Message}");
            return null;
        }
    }

    /// <summary>"43.25°N 76.92°E", when the name of the place is unknown.</summary>
    public static string Coordinates(double latitude, double longitude) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{Math.Abs(latitude):0.00}°{(latitude >= 0 ? 'N' : 'S')} {Math.Abs(longitude):0.00}°{(longitude >= 0 ? 'E' : 'W')}");

    /// <summary>City name of the point. Not every platform can do it (Windows needs a map key),
    /// so the weather is shown for the coordinates anyway.</summary>
    private async Task<Placemark?> FindPlacemarkAsync(Location location)
    {
        try
        {
            var placemarks = await geocoding.GetPlacemarksAsync(location);
            return placemarks?.FirstOrDefault();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
