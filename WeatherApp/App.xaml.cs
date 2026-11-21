namespace WeatherApp;

public partial class App : Application
{
    private readonly IServiceProvider services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        this.services = services;
    }

    // The page is created here, after InitializeComponent, so it can use the converters of App.xaml.
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(services.GetRequiredService<MainPage>())
        {
            Title = "Weather",
            // a phone-like window on Windows and macOS
            Width = 430,
            Height = 900,
            MinimumWidth = 380,
            MinimumHeight = 700,
        };
}
