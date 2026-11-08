using System;
using System.IO;
using System.Web;
using Eto.Forms;

namespace SkybloxLauncher;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Skyblox", "launcher_log.txt");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.WriteAllText(logPath, $"Launcher starting at {DateTime.Now}\n");
            File.AppendAllText(logPath, $"Args: {string.Join(" ", args)}\n");

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

            File.AppendAllText(logPath, "Initializing platform...\n");
#if WINDOWS_PLATFORM
            var platform = new Eto.Wpf.Platform();
#elif MAC_PLATFORM
            var platform = new Eto.Mac.Platform();
#else
            var platform = Eto.Platform.Detect;
#endif
            File.AppendAllText(logPath, $"Platform initialized: {platform.GetType().FullName}\n");

            var app = new Application(platform);
            File.AppendAllText(logPath, "Running application...\n");
            app.Run(new MainForm(p, t, y));
            File.AppendAllText(logPath, "Application exited cleanly.\n");
        }
        catch (Exception ex)
        {
            File.AppendAllText(logPath, $"FATAL EXCEPTION: {ex}\n");
        }
    }
}
