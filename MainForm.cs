using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Eto.Drawing;
using Eto.Forms;

namespace SkybloxLauncher;

public class MainForm : Form
{
#if DEBUG
    private const string CurrentVersion = "DEBUG";
#else
    private const string CurrentVersion = "1.0.6";
#endif
    private const string BaseUrl = "https://skyblox.co";
    private const string LauncherDownloadUrl = BaseUrl + "/clients/SkybloxLauncher.exe";
    private const string LauncherDownloadUrlMac = BaseUrl + "/clients/OSX/SkybloxLauncher-macOS.zip";
    private const string DeployHistoryUrl = BaseUrl + "/clients/DeployHistory.txt";

    private readonly string? placeId;
    private readonly string? ticket;
    private readonly string year;

    private readonly Label status;
    private readonly ProgressBar progress;
    private readonly Label closeBtn;
    private readonly Label repairLink;
    private readonly Label footer;

    private bool isDarkMode = true;
    private bool isRepairMode = false;

    private PointF dragStart;
    private bool isDragging = false;

    private static string AppDataFolder
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Skyblox");
            }
            else if (OperatingSystem.IsMacOS())
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Skyblox");
            }
            else
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "Skyblox");
            }
        }
    }

    private string CurrentYearFolder => Path.Combine(AppDataFolder, year.Contains("2021") ? "2021" : year.Contains("2020") ? "2020" : year.Contains("2019") ? "2019" : year.Contains("2018") ? "2018" : year.Contains("2017") ? "2017" : year.Contains("2015") ? "2015" : "2016");
    private string ClientExe => Path.Combine(CurrentYearFolder, "SkybloxPlayerBeta.exe");

    public MainForm() : this(null, null, "2016") { }

    public MainForm(string? placeId, string? ticket, string year)
    {
        string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Skyblox", "launcher_log.txt");
        File.AppendAllText(logPath, "MainForm ctor starting...\n");
        this.placeId = placeId;
        this.ticket = ticket;
        this.year = year;

        Title = "Skyblox Launcher";
        ClientSize = new Size(440, 280);
        WindowStyle = WindowStyle.None;
        Resizable = false;

        LoadComplete += (s, e) =>
        {
            File.AppendAllText(logPath, "MainForm LoadComplete event fired\n");
            try
            {
                var screen = Screen.PrimaryScreen;
                if (screen != null)
                {
                    var wa = screen.WorkingArea;
                    Location = new Point((int)(wa.X + (wa.Width - ClientSize.Width) / 2), (int)(wa.Y + (wa.Height - ClientSize.Height) / 2));
                    File.AppendAllText(logPath, $"Set location to {Location}\n");
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(logPath, $"LoadComplete error: {ex}\n");
            }
        };

        try
        {
            Icon = Icon.FromResource("SkybloxLauncher.Resources.SkybloxLogo.ico");
        }
        catch { }

        // Window drag handling
        MouseDown += (s, e) =>
        {
            if (e.Buttons == MouseButtons.Primary)
            {
                isDragging = true;
                dragStart = e.Location;
            }
        };
        MouseMove += (s, e) =>
        {
            if (isDragging)
            {
                var diff = e.Location - dragStart;
                Location = new Point((int)(Location.X + diff.X), (int)(Location.Y + diff.Y));
            }
        };
        MouseUp += (s, e) => { isDragging = false; };

        // Logo
        Image? logoImg = null;
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string p1 = Path.Combine(baseDir, "Skyblox_logo.png");
            string p2 = Path.Combine(baseDir, "Resources", "Skyblox_logo.png");
            if (File.Exists(p1)) logoImg = new Bitmap(p1);
            else if (File.Exists(p2)) logoImg = new Bitmap(p2);
            else if (File.Exists("Skyblox_logo.png")) logoImg = new Bitmap("Skyblox_logo.png");
            else if (File.Exists("Resources/Skyblox_logo.png")) logoImg = new Bitmap("Resources/Skyblox_logo.png");
            else logoImg = Bitmap.FromResource("SkybloxLauncher.Resources.Skyblox_logo.png");
        }
        catch { }

        var logo = new ImageView
        {
            Image = logoImg,
            Size = new Size(180, 90),
            Cursor = Cursors.Pointer
        };
        logo.MouseDown += (s, e) =>
        {
            isDarkMode = !isDarkMode;
            ApplyTheme();
        };

        status = new Label
        {
            Text = "Checking for updates...",
            Size = new Size(440, 45),
            TextAlignment = TextAlignment.Center,
            Font = Fonts.Sans(11, FontStyle.Bold)
        };

        progress = new ProgressBar
        {
            Size = new Size(300, 6),
            Indeterminate = true
        };

        closeBtn = new Label
        {
            Text = "✕",
            Size = new Size(25, 25),
            Font = Fonts.Sans(12, FontStyle.Bold),
            Cursor = Cursors.Pointer,
            TextAlignment = TextAlignment.Center
        };
        closeBtn.MouseDown += (s, e) => Application.Instance.Quit();

        repairLink = new Label
        {
            Text = "Repair",
            Size = new Size(440, 18),
            TextAlignment = TextAlignment.Center,
            Font = Fonts.Sans(9),
            Cursor = Cursors.Pointer
        };
        repairLink.MouseDown += (s, e) =>
        {
            isRepairMode = true;
            Task.Run(StartLauncher);
        };

        footer = new Label
        {
            Text = $"v{CurrentVersion}",
            Size = new Size(440, 18),
            TextAlignment = TextAlignment.Center,
            Font = Fonts.Sans(8)
        };

        var layout = new PixelLayout();
        layout.Add(logo, 130, 25);
        layout.Add(status, 0, 125);
        layout.Add(progress, 70, 180);
        layout.Add(closeBtn, 405, 10);
        layout.Add(repairLink, 0, 230);
        layout.Add(footer, 0, 250);

        Content = layout;
        ApplyTheme();

        File.AppendAllText(logPath, "MainForm layout and theme set, registering Shown\n");

        Shown += (s, e) =>
        {
            File.AppendAllText(logPath, "MainForm Shown event fired!\n");
            Task.Run(StartLauncher);
        };
        File.AppendAllText(logPath, "MainForm ctor completed successfully!\n");
    }

    private void ApplyTheme()
    {
        Color bg = isDarkMode ? Color.FromArgb(25, 25, 25) : Color.FromArgb(245, 245, 245);
        Color text = isDarkMode ? Color.FromArgb(240, 240, 240) : Color.FromArgb(40, 40, 40);
        BackgroundColor = bg;
        status.TextColor = text;
        closeBtn.TextColor = text;
        footer.TextColor = Color.FromArgb(120, 120, 120);
        repairLink.TextColor = Color.FromArgb(100, 149, 237);
    }

    private void UpdateStatus(string message)
    {
        Application.Instance.AsyncInvoke(() =>
        {
            status.Text = message;
        });
    }

    private void SetProgress(int percent, long currentBytes, long totalBytes)
    {
        Application.Instance.AsyncInvoke(() =>
        {
            progress.Indeterminate = false;
            progress.Value = Math.Clamp(percent, 0, 100);
            if (totalBytes > 0)
            {
                string firstLine = status.Text?.Split('\n')[0] ?? "";
                status.Text = $"{firstLine}\n{currentBytes / 1024 / 1024} MB / {totalBytes / 1024 / 1024} MB ({percent}%)";
            }
        });
    }

    private void SetIndeterminate(string message)
    {
        Application.Instance.AsyncInvoke(() =>
        {
            progress.Indeterminate = true;
            status.Text = message;
        });
    }

    private async Task StartLauncher()
    {
        try
        {
            UpdateStatus("Checking for updates...");
            await CheckForLauncherUpdates();

            if (!Directory.Exists(AppDataFolder)) Directory.CreateDirectory(AppDataFolder);
            RegisterProtocol();

            await InstallAllMissingClients();

            if (!string.IsNullOrEmpty(placeId))
            {
                UpdateStatus("All clients up to date!\nLaunching...");
                await Task.Delay(800);
                LaunchGame();
            }
            else
            {
                UpdateStatus("Finished installing all clients. You may now exit");
            }
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error: {ex.Message}");
        }
    }

    private async Task CheckForLauncherUpdates()
    {
        try
        {
            using var client = new HttpClient();
            string history = await client.GetStringAsync($"{DeployHistoryUrl}?t={DateTime.Now.Ticks}");
            string prefix = OperatingSystem.IsMacOS() ? "LauncherOSX:" : "Launcher:";
            string remoteHash = "";

            foreach (var line in history.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith(prefix)) { remoteHash = line.Trim(); break; }
            }
            if (string.IsNullOrEmpty(remoteHash))
            {
                foreach (var line in history.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.StartsWith("Launcher:")) { remoteHash = line.Trim(); break; }
                }
            }

            if (string.IsNullOrEmpty(remoteHash)) return;

            string localHistoryPath = Path.Combine(AppDataFolder, "DeployHistory.txt");
            string localHash = "";
            if (File.Exists(localHistoryPath))
            {
                foreach (var line in File.ReadAllText(localHistoryPath).Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.StartsWith(prefix)) { localHash = line.Trim(); break; }
                    if (string.IsNullOrEmpty(localHash) && line.StartsWith("Launcher:")) { localHash = line.Trim(); }
                }
            }

            if (OperatingSystem.IsWindows() && !string.IsNullOrEmpty(localHash) && localHash != remoteHash)
            {
                UpdateStatus("Updating launcher...");
                byte[] newExe = await client.GetByteArrayAsync(LauncherDownloadUrl);
                string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (!string.IsNullOrEmpty(currentExe))
                {
                    string tmpPath = currentExe + ".tmp";
                    await File.WriteAllBytesAsync(tmpPath, newExe);

                    if (!Directory.Exists(AppDataFolder)) Directory.CreateDirectory(AppDataFolder);
                    await File.WriteAllTextAsync(localHistoryPath, history.Trim());

                    string batch = $"@echo off\ntimeout /t 1\ndel \"{currentExe}\"\nmove \"{tmpPath}\" \"{currentExe}\"\nstart \"\" \"{currentExe}\"\nexit";
                    await File.WriteAllTextAsync("update.bat", batch);
                    Process.Start(new ProcessStartInfo("update.bat") { CreateNoWindow = true, UseShellExecute = false });
                    Application.Instance.AsyncInvoke(() => Application.Instance.Quit());
                }
            }
            else if (OperatingSystem.IsMacOS() && !string.IsNullOrEmpty(localHash) && localHash != remoteHash)
            {
                string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                int appIdx = currentExe.IndexOf(".app", StringComparison.OrdinalIgnoreCase);
                if (appIdx != -1)
                {
                    string appBundlePath = currentExe.Substring(0, appIdx + 4);
                    if (!appBundlePath.StartsWith("/Volumes/"))
                    {
                        UpdateStatus("Updating macOS launcher...");
                        string updateZip = Path.Combine(AppDataFolder, "SkybloxLauncher_update.zip");
                        string extractDir = Path.Combine(AppDataFolder, "update_extracted");

                        if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                        if (File.Exists(updateZip)) File.Delete(updateZip);

                        byte[] newZip = await client.GetByteArrayAsync(LauncherDownloadUrlMac);
                        await File.WriteAllBytesAsync(updateZip, newZip);

                        ZipFile.ExtractToDirectory(updateZip, extractDir);
                        File.Delete(updateZip);

                        string newAppPath = Path.Combine(extractDir, "SkybloxLauncher.app");
                        if (!Directory.Exists(newAppPath))
                        {
                            var dirs = Directory.GetDirectories(extractDir, "*.app", SearchOption.AllDirectories);
                            if (dirs.Length > 0) newAppPath = dirs[0];
                        }

                        if (Directory.Exists(newAppPath))
                        {
                            if (!Directory.Exists(AppDataFolder)) Directory.CreateDirectory(AppDataFolder);
                            await File.WriteAllTextAsync(localHistoryPath, history.Trim());

                            string scriptPath = Path.Combine(AppDataFolder, "update.sh");
                            string scriptContent = $@"#!/bin/bash
sleep 1
rm -rf ""{appBundlePath}""
cp -R ""{newAppPath}"" ""{appBundlePath}""
rm -rf ""{extractDir}""
xattr -cr ""{appBundlePath}"" 2>/dev/null || true
open ""{appBundlePath}""
rm -f ""$0""
";
                            await File.WriteAllTextAsync(scriptPath, scriptContent);
                            File.SetUnixFileMode(scriptPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

                            Process.Start(new ProcessStartInfo("/bin/bash", $"\"{scriptPath}\"") { UseShellExecute = false });
                            Application.Instance.AsyncInvoke(() => Application.Instance.Quit());
                        }
                    }
                }
            }
        }
        catch { }
    }

    private async Task InstallAllMissingClients()
    {
        string[] years = { "2015", "2016", "2017", "2018", "2019", "2020" };

        using var client = new HttpClient();
        string remoteHistory = "";
        try { remoteHistory = await client.GetStringAsync(DeployHistoryUrl); } catch { }

        string localHistoryPath = Path.Combine(AppDataFolder, "DeployHistory.txt");
        string localHistory = File.Exists(localHistoryPath) ? File.ReadAllText(localHistoryPath) : "";

        for (int i = 0; i < years.Length; i++)
        {
            bool isTargetYear = this.year.Contains(years[i]);
            string path = Path.Combine(AppDataFolder, years[i]);
            string exePath = Path.Combine(path, "SkybloxPlayerBeta.exe");
            string urlZip = $"{BaseUrl}/clients/{years[i].Substring(2, 2)}client.zip";

            if (!isTargetYear && !isRepairMode && !File.Exists(exePath)) continue;

            string remoteHash = "";
            if (!string.IsNullOrEmpty(remoteHistory))
            {
                foreach (var line in remoteHistory.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.StartsWith(years[i] + ":")) { remoteHash = line.Trim(); break; }
                }
            }

            string localHash = "";
            if (!string.IsNullOrEmpty(localHistory))
            {
                foreach (var line in localHistory.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.StartsWith(years[i] + ":")) { localHash = line.Trim(); break; }
                }
            }

            bool hashMismatch = !string.IsNullOrEmpty(remoteHash) && remoteHash != localHash;

            if (!File.Exists(exePath) || (isRepairMode && isTargetYear) || hashMismatch)
            {
                bool isUpdate = File.Exists(exePath);
                string actionStr = isUpdate ? "Updating" : "Downloading";
                SetIndeterminate($"{actionStr} {years[i]}...");

                string zip = Path.Combine(AppDataFolder, $"temp_{years[i]}.zip");

                try
                {
                    await DownloadFile(client, urlZip, zip, years[i], actionStr);
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Failed to download {years[i]}: {ex.Message}");
                    if (File.Exists(zip)) File.Delete(zip);
                    continue;
                }

                SetIndeterminate($"Extracting {years[i]}...");
                if (Directory.Exists(path)) Directory.Delete(path, true);
                ZipFile.ExtractToDirectory(zip, path);
                File.Delete(zip);

                if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
                {
                    try
                    {
                        if (File.Exists(exePath))
                        {
                            File.SetUnixFileMode(exePath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                        }
                    }
                    catch { }
                }
            }
        }

        if (!string.IsNullOrEmpty(remoteHistory))
        {
            await File.WriteAllTextAsync(localHistoryPath, remoteHistory.Trim());
        }
    }

    private async Task DownloadFile(HttpClient client, string url, string dest, string yearLabel, string actionStr)
    {
        using var res = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        res.EnsureSuccessStatusCode();

        var total = res.Content.Headers.ContentLength ?? -1L;
        using var fs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None);
        using var s = await res.Content.ReadAsStreamAsync();

        byte[] buffer = new byte[16384];
        long readTotal = 0;
        int read;

        while ((read = await s.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await fs.WriteAsync(buffer.AsMemory(0, read));
            readTotal += read;
            if (total != -1)
            {
                int pct = (int)((readTotal * 100) / total);
                SetProgress(pct, readTotal, total);
            }
            else
            {
                SetIndeterminate($"{actionStr} {yearLabel}...\n{readTotal / 1024 / 1024} MB");
            }
        }
    }

    private void LaunchGame()
    {
        if (!File.Exists(ClientExe))
        {
            UpdateStatus($"Missing: {ClientExe}\nClick 'Repair' to redownload.");
            return;
        }

        string? yearFlag = year.Contains("2021") ? "2021" : year.Contains("2020") ? "2020" : year.Contains("2019") ? "2019" : year.Contains("2018") ? "2018" : year.Contains("2017") ? "2017" : year.Contains("2015") ? "2015" : null;
        string joinUrl = !string.IsNullOrEmpty(yearFlag)
            ? $"{BaseUrl}/game/PlaceLauncher.ashx?placeid={placeId}&ticket={ticket}&{yearFlag}=true"
            : $"{BaseUrl}/game/PlaceLauncher.ashx?placeid={placeId}&ticket={ticket}";

        string args = $"-a \"{BaseUrl.Replace("https://", "http://")}/Login/Negotiate.ashx\" -j \"{joinUrl.Replace("https://", "http://")}\" -t \"{ticket}\"";

        try
        {
            if (!string.IsNullOrEmpty(placeId) && !string.IsNullOrEmpty(ticket))
            {
                DiscordManager.Initialize(placeId, year, ticket);
            }

            if (OperatingSystem.IsWindows())
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ClientExe,
                    Arguments = args,
                    WorkingDirectory = Path.GetDirectoryName(ClientExe),
                    UseShellExecute = false
                };
                Process.Start(psi);
            }
            else if (OperatingSystem.IsMacOS())
            {
                string wineCmd = "wine";
                string[] possibleWinePaths = {
                    "/usr/local/bin/wine",
                    "/opt/homebrew/bin/wine",
                    "/Applications/Wine Stable.app/Contents/Resources/wine/bin/wine"
                };
                foreach (var p in possibleWinePaths)
                {
                    if (File.Exists(p)) { wineCmd = p; break; }
                }

                var psi = new ProcessStartInfo
                {
                    FileName = wineCmd,
                    Arguments = $"\"{ClientExe}\" {args}",
                    WorkingDirectory = Path.GetDirectoryName(ClientExe),
                    UseShellExecute = false
                };
                Process.Start(psi);
            }
            else
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "wine",
                    Arguments = $"\"{ClientExe}\" {args}",
                    WorkingDirectory = Path.GetDirectoryName(ClientExe),
                    UseShellExecute = false
                };
                Process.Start(psi);
            }

            Application.Instance.AsyncInvoke(() => this.Visible = false);

            new Thread(() =>
            {
                Thread.Sleep(5000);
                while (Process.GetProcessesByName("SkybloxPlayerBeta").Length > 0)
                {
                    Thread.Sleep(3000);
                }
                DiscordManager.Shutdown();
                Application.Instance.AsyncInvoke(() => Application.Instance.Quit());
            })
            { IsBackground = true }.Start();
        }
        catch (Exception ex)
        {
            UpdateStatus($"Launch Error: {ex.Message}");
        }
    }

    private void RegisterProtocol()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Classes\sclient");
                k.SetValue("", "URL:Skyblox Protocol");
                k.SetValue("URL Protocol", "");
                string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (!string.IsNullOrEmpty(currentExe))
                {
                    k.CreateSubKey(@"shell\open\command").SetValue("", $"\"{currentExe}\" \"%1\"");
                }
            }
            catch { }
        }
    }
}
