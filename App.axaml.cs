using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace SkybloxLauncher;

public partial class App : Application
{
    public static string? PlaceId { get; set; }
    public static string? Ticket { get; set; }
    public static string Year { get; set; } = "2016";

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(PlaceId, Ticket, Year);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
