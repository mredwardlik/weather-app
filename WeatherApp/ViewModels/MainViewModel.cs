using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WeatherApp.Core;
using WeatherApp.Services;

namespace WeatherApp.ViewModels;

public sealed record DayItem(string DayName, string Date, string Icon, string Max, string Min, double Opacity);

public sealed partial class MainViewModel(IWeatherClient weather, ILocationService location, IPreferences preferences)
    : ObservableObject
{
    private const string PlaceKey = "place";
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    private Place place = Place.Almaty;
    private CancellationTokenSource? searchCancel;
    private Func<Task>? retry;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsFirstLoading))]
    public partial bool IsLoaded { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }

    /// <summary>Set by the pull-to-refresh gesture only. Setting it from code would start a refresh.</summary>
    [ObservableProperty] public partial bool IsRefreshing { get; set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsFirstLoading))]
    public partial string? Error { get; set; }

    /// <summary>Nothing to show yet and no error: the spinner in the middle.</summary>
    public bool IsFirstLoading => !IsLoaded && Error is null;

    [ObservableProperty] public partial string City { get; set; } = "";
    [ObservableProperty] public partial string Date { get; set; } = "";
    [ObservableProperty] public partial string Description { get; set; } = "";
    [ObservableProperty] public partial string Icon { get; set; } = "w03d.png";
    [ObservableProperty] public partial string Temperature { get; set; } = "";
    [ObservableProperty] public partial string FeelsLike { get; set; } = "";
    [ObservableProperty] public partial string Humidity { get; set; } = "";
    [ObservableProperty] public partial string Wind { get; set; } = "";
    [ObservableProperty] public partial string Pressure { get; set; } = "";
    [ObservableProperty] public partial string Cloudiness { get; set; } = "";
    [ObservableProperty] public partial string Updated { get; set; } = "";
    [ObservableProperty] public partial Color SkyTop { get; set; } = SkyColors.Default.Top;
    [ObservableProperty] public partial Color SkyBottom { get; set; } = SkyColors.Default.Bottom;

    /// <summary>The next four days. Replaced as a whole, so bindings like Days[0] update.</summary>
    [ObservableProperty] public partial IReadOnlyList<DayItem> Days { get; set; } = [];

    [ObservableProperty] public partial bool IsSearchOpen { get; set; }
    [ObservableProperty] public partial bool IsSearching { get; set; }
    [ObservableProperty] public partial string SearchText { get; set; } = "";
    [ObservableProperty] public partial string? SearchMessage { get; set; }
    public ObservableCollection<Place> SearchResults { get; } = [];
    [ObservableProperty] public partial Place? SelectedResult { get; set; }

    public DateTime LastUpdate { get; private set; } = DateTime.MinValue;

    /// <summary>The saved city, or the current location, or Almaty.</summary>
    [RelayCommand]
    public async Task StartAsync()
    {
        var saved = LoadPlace();
        if (saved is not null)
        {
            place = saved;
        }
        else
        {
            IsBusy = true;
            place = await location.GetCurrentPlaceAsync() ?? Place.Almaty;
        }
        await RefreshAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            Show(await weather.GetForecastAsync(place));
            Error = null;
        }
        catch (WeatherException error)
        {
            Error = error.Message;
            retry = RefreshAsync;
        }
        finally
        {
            IsBusy = false;
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task UseMyLocationAsync()
    {
        IsSearchOpen = false;
        IsBusy = true;
        var current = await location.GetCurrentPlaceAsync();
        if (current is null)
        {
            IsBusy = false;
            Error = "Location is not available. Allow it in the settings or search for a city.";
            retry = UseMyLocationAsync;
            return;
        }
        await SelectPlaceAsync(current);
    }

    /// <summary>The button on the error: repeats what failed.</summary>
    [RelayCommand]
    private Task RetryAsync() => (retry ?? RefreshAsync)();

    [RelayCommand]
    private void OpenSearch()
    {
        SearchText = "";
        SearchResults.Clear();
        SearchMessage = "Type a city name";
        IsSearchOpen = true;
    }

    [RelayCommand]
    private void CloseSearch() => IsSearchOpen = false;

    [RelayCommand]
    private async Task SelectPlaceAsync(Place? selected)
    {
        if (selected is null)
            return;
        IsSearchOpen = false;
        place = selected;
        SavePlace(selected);
        await RefreshAsync();
    }

    partial void OnSelectedResultChanged(Place? value)
    {
        if (value is null)
            return;
        SelectedResult = null;
        _ = SelectPlaceAsync(value);
    }

    /// <summary>Enter in the search box: the first city of the list.</summary>
    [RelayCommand]
    private async Task SelectFirstResultAsync()
    {
        if (IsSearching || SearchResults.Count == 0)
            await SearchAsync(SearchText, delay: 0);
        if (SearchResults.Count > 0)
            await SelectPlaceAsync(SearchResults[0]);
    }

    /// <summary>Search while typing, 400 ms after the last key.</summary>
    partial void OnSearchTextChanged(string value) => _ = SearchAsync(value);

    public async Task SearchAsync(string query, int delay = 400)
    {
        searchCancel?.Cancel();
        var cancel = searchCancel = new CancellationTokenSource();
        try
        {
            await Task.Delay(delay, cancel.Token);
            if (query.Trim().Length < 2)
            {
                SearchResults.Clear();
                SearchMessage = "Type a city name";
                return;
            }

            IsSearching = true;
            var found = await weather.SearchAsync(query, cancel.Token);
            cancel.Token.ThrowIfCancellationRequested();
            SearchResults.Clear();
            foreach (var item in found)
                SearchResults.Add(item);
            SearchMessage = found.Count == 0 ? $"Nothing found for “{query.Trim()}”" : null;
        }
        catch (OperationCanceledException)
        {
            // a newer search has started
        }
        catch (WeatherException error)
        {
            SearchResults.Clear();
            SearchMessage = error.Message;
        }
        finally
        {
            if (searchCancel == cancel)
                IsSearching = false;
        }
    }

    private void Show(Forecast forecast)
    {
        var now = forecast.Current;
        var condition = now.Condition;

        City = forecast.Place.Name.ToUpper(English);
        Date = now.Time.ToString("dddd, MMM d", English).ToUpper(English);
        Description = condition.Description.ToUpper(English);
        Icon = condition.IconFile(now.IsDay);
        Temperature = Degrees(now.Temperature);
        FeelsLike = $"FEELS LIKE {Degrees(now.FeelsLike)}°";
        Humidity = $"{now.Humidity}%";
        Wind = $"{now.WindSpeed.ToString("0.#", English)} m/s";
        Pressure = $"{now.Pressure:0} hPa";
        Cloudiness = $"{now.CloudCover}%";
        Updated = $"Updated at {now.Time:HH:mm} local time";

        var sky = SkyColors.For(condition.Sky, now.IsDay);
        SkyTop = sky.Top;
        SkyBottom = sky.Bottom;

        Days = forecast.NextDays(4)
            .Select((day, i) => new DayItem(
                day.Date.ToString("dddd", English),
                day.Date.ToString("dd MMM", English),
                day.Condition.IconFile(isDay: true),
                Degrees(day.MaxTemperature),
                Degrees(day.MinTemperature),
                0.4 + 0.2 * i))  // the tiles get brighter to the right, as in the first version
            .ToList();

        LastUpdate = DateTime.Now;
        IsLoaded = true;
    }

    /// <summary>"-0" looks odd, so it is shown as "0".</summary>
    private static string Degrees(double value)
    {
        var rounded = Math.Round(value, MidpointRounding.AwayFromZero);
        return (rounded == 0 ? 0 : rounded).ToString("0", English);
    }

    private Place? LoadPlace()
    {
        try
        {
            var json = preferences.Get<string?>(PlaceKey, null);
            return json is null ? null : JsonSerializer.Deserialize<Place>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void SavePlace(Place value) => preferences.Set(PlaceKey, JsonSerializer.Serialize(value));
}
