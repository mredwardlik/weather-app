using WeatherApp.ViewModels;

namespace WeatherApp;

public partial class MainPage : ContentPage
{
    /// <summary>The weather is loaded again when the app comes back after this time.</summary>
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(15);

    private readonly MainViewModel model;
    private readonly IDispatcherTimer timer;
    private bool started;

    public MainPage(MainViewModel model)
    {
        InitializeComponent();
        BindingContext = this.model = model;

        timer = Dispatcher.CreateTimer();
        timer.Interval = RefreshInterval;
        timer.Tick += async (_, _) => await model.RefreshAsync();

        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsSearchOpen) && model.IsSearchOpen)
                Dispatcher.Dispatch(() => SearchEntry.Focus());
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        timer.Start();
        if (!started)
        {
            started = true;
            await model.StartAsync();
        }
        else if (DateTime.Now - model.LastUpdate > RefreshInterval)
        {
            await model.RefreshAsync();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        timer.Stop();
    }

    protected override bool OnBackButtonPressed()
    {
        if (!model.IsSearchOpen)
            return base.OnBackButtonPressed();
        model.CloseSearchCommand.Execute(null);
        return true;
    }
}
