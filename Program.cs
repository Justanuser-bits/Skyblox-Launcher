using Avalonia;
using System;
using System.Web;

namespace SkybloxLauncher;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        string? p = null;
        string? t = null;
        string y = "2016";

        if (args.Length > 0 && args[0].StartsWith("sclient://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(args[0]);
                var q = HttpUtility.ParseQueryString(uri.Query);
                p = q["place"] ?? q["placeId"];
                t = q["ticket"];
                y = (q["2021"] == "true" || q["year"] == "2021") ? "2021" :
                    (q["2020"] == "true" || q["year"] == "2020") ? "2020" :
                    (q["2019"] == "true" || q["year"] == "2019") ? "2019" :
                    (q["2018"] == "true" || q["year"] == "2018") ? "2018" :
                    (q["2017"] == "true" || q["year"] == "2017") ? "2017" :
                    (q["2015"] == "true" || q["year"] == "2015") ? "2015" : "2016";
            }
            catch { }
        }

        App.PlaceId = p;
        App.Ticket = t;
        App.Year = y;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
