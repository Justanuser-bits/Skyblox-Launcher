using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SkybloxLauncher
{
    public partial class LauncherForm : Form
    {

#if DEBUG
        private const string CurrentVersion = "DEBUG";
#else
        private const string CurrentVersion = "1.0.5";
#endif        
        private const string BaseUrl             = "https://skyblox.co";
        
        private const string LauncherDownloadUrl = BaseUrl + "/clients/SkybloxLauncher.exe";
        private const string DeployHistoryUrl    = BaseUrl + "/clients/DeployHistory.txt";

        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;
        [DllImport("user32.dll")] public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")] public static extern bool ReleaseCapture();

        private readonly string placeId, ticket, year;
        private readonly string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Skyblox");

        private string CurrentYearFolder => Path.Combine(appData, year.Contains("2021") ? "2021" : year.Contains("2020") ? "2020" : year.Contains("2019") ? "2019" : year.Contains("2018") ? "2018" : year.Contains("2017") ? "2017" : year.Contains("2015") ? "2015" : "2016");
        private string ClientExe => Path.Combine(CurrentYearFolder, "SkybloxPlayerBeta.exe");
        private string AppExePath => Application.ExecutablePath;
        
        private ProgressBar progress;
        private Label status, closeBtn, repairLink;
        private bool isDarkMode = false;
        private bool isRepairMode = false;

        public LauncherForm(string placeId, string ticket, string year)
        {
            this.placeId = placeId;
            this.ticket = ticket;
            this.year = year;
            if (Control.ModifierKeys == Keys.Shift) isRepairMode = true;

            InitializeComponent();
            this.Load += (s, e) => Task.Run(StartLauncher);
        }

        private async Task StartLauncher()
        {
            try
            {
                UpdateStatus("Checking for updates...");
                await CheckForLauncherUpdates();

                if (!Directory.Exists(appData)) Directory.CreateDirectory(appData);
                RegisterProtocol();

                await InstallAllMissingClients();

                if (!string.IsNullOrEmpty(placeId))
                {
                    UpdateStatus("All clients up to date!\nLaunching...");
                    await Task.Delay(800);
                    LaunchGame();
                }
                else
                    UpdateStatus("Finished installing all clients. You may now exit");
            }
            catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
        }

                private async Task CheckForLauncherUpdates()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    string history = await client.GetStringAsync($"{DeployHistoryUrl}?t={DateTime.Now.Ticks}");
                    string remoteHash = "";
                    foreach (var line in history.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                        if (line.StartsWith("Launcher:")) { remoteHash = line.Trim(); break; }

                    if (string.IsNullOrEmpty(remoteHash)) return;

                    string localHistoryPath = Path.Combine(appData, "DeployHistory.txt");
                    string localHash = "";
                    if (File.Exists(localHistoryPath)) {
                        foreach (var line in File.ReadAllText(localHistoryPath).Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                            if (line.StartsWith("Launcher:")) { localHash = line.Trim(); break; }
                    }

#if !DEBUG
                    if (localHash != remoteHash)
                    {
                        UpdateStatus($"Updating launcher...");
                        byte[] newExe = await client.GetByteArrayAsync(LauncherDownloadUrl);
                        string tmpPath = AppExePath + ".tmp";
                        File.WriteAllBytes(tmpPath, newExe);
                        
                        // Write the full DeployHistory directly from C# before exiting
                        if (!Directory.Exists(appData)) Directory.CreateDirectory(appData);
                        File.WriteAllText(localHistoryPath, history.Trim());
                        
                        // Cleanup old file
                        string oldHashPath = Path.Combine(appData, "LauncherHash.txt");
                        if (File.Exists(oldHashPath)) File.Delete(oldHashPath);

                        string batch = $"@echo off\ntimeout /t 1\ndel \"{AppExePath}\"\nmove \"{tmpPath}\" \"{AppExePath}\"\nstart \"\" \"{AppExePath}\"\nexit";
                        File.WriteAllText("update.bat", batch);
                        Process.Start(new ProcessStartInfo("update.bat") { CreateNoWindow = true, UseShellExecute = false });
                        Application.Exit();
                    }
#endif
                }
            }
            catch { }
        }

        private async Task InstallAllMissingClients()
        {
            string[] years = { "2015", "2016", "2017", "2018", "2019", "2020" };
            
            using (var client = new HttpClient())
            {
                string remoteHistory = "";
                try { remoteHistory = await client.GetStringAsync(DeployHistoryUrl); } catch { }

                string localHistoryPath = Path.Combine(appData, "DeployHistory.txt");
                string localHistory = File.Exists(localHistoryPath) ? File.ReadAllText(localHistoryPath) : "";

                for (int i = 0; i < years.Length; i++)
                {
                    bool isTargetYear = this.year.Contains(years[i]);
                    string path     = Path.Combine(appData, years[i]);
                    string exePath  = Path.Combine(path, "SkybloxPlayerBeta.exe");
                    string urlZip = $"{BaseUrl}/clients/{years[i].Substring(2, 2)}client.zip";

                    // Skip years that aren't installed AND aren't the launching year (unless repairing)
                    if (!isTargetYear && !isRepairMode && !File.Exists(exePath)) continue;

                    string remoteHash = "";
                    if (!string.IsNullOrEmpty(remoteHistory))
                        foreach (var line in remoteHistory.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                            if (line.StartsWith(years[i] + ":")) { remoteHash = line.Trim(); break; }

                    string localHash = "";
                    if (!string.IsNullOrEmpty(localHistory))
                        foreach (var line in localHistory.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                            if (line.StartsWith(years[i] + ":")) { localHash = line.Trim(); break; }

                    bool hashMismatch = !string.IsNullOrEmpty(remoteHash) && remoteHash != localHash;

                    if (!File.Exists(exePath) || (isRepairMode && year.Contains(years[i])) || hashMismatch)
                    {
                        bool isUpdate   = File.Exists(exePath);
                        string actionStr = isUpdate ? "Updating" : "Downloading";
                        UpdateStatus($"{actionStr} {years[i]}...");

                        string zip = Path.Combine(appData, $"temp_{years[i]}.zip");

                        try
                        {
                            await DownloadFile(urlZip, zip, years[i], actionStr);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(
                                $"Failed to download the {years[i]} client.\n\n{ex.Message}\n\nPlease check your internet connection and try again.",
                                "Download Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                            if (File.Exists(zip)) File.Delete(zip);
                            continue;
                        }

                        UpdateStatus($"Extracting {years[i]}...");
                        if (Directory.Exists(path)) Directory.Delete(path, true);
                        ZipFile.ExtractToDirectory(zip, path);
                        File.Delete(zip);
                    }
                }

                //Always write the full remote DeployHistory.txt locally after the loop keeps the local copy in sync even when nothing was re-downloaded.
                if (!string.IsNullOrEmpty(remoteHistory))
                    File.WriteAllText(localHistoryPath, remoteHistory.Trim());
            }
        }

        private async Task DownloadFile(string url, string dest, string yearLabel, string actionStr)
        {
            using (var client = new HttpClient())
            using (var res = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                res.EnsureSuccessStatusCode();
                var total = res.Content.Headers.ContentLength ?? -1L;
                using (var fs = new FileStream(dest, FileMode.Create))
                using (var s = await res.Content.ReadAsStreamAsync())
                {
                    byte[] buffer = new byte[8192];
                    long readTotal = 0; int read;
                    while ((read = await s.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fs.WriteAsync(buffer, 0, read);
                        readTotal += read;
                        if (total != -1)
                        {
                            int pct = (int)((readTotal * 100) / total);
                            BeginInvoke((MethodInvoker)(() => {
                                progress.Style = ProgressBarStyle.Blocks;
                                progress.Value = pct;
                                status.Text = $"{actionStr} {yearLabel}... {pct}%\n{readTotal / 1024 / 1024} MB / {total / 1024 / 1024} MB";
                            }));
                        }
                        else
                        {
                            BeginInvoke((MethodInvoker)(() => {
                                progress.Style = ProgressBarStyle.Marquee;
                                status.Text = $"{actionStr} {yearLabel}...\n{readTotal / 1024 / 1024} MB";
                            }));
                        }
                    }
                }
            }
        }

        private void LaunchGame()
        {
            if (!File.Exists(ClientExe))
            {
                MessageBox.Show($"Missing: {ClientExe}\nClick 'Repair' in the launcher footer or hold Shift while opening.");
                return;
            }

            string yearFlag = year.Contains("2021") ? "2021" : year.Contains("2020") ? "2020" : year.Contains("2019") ? "2019" : year.Contains("2018") ? "2018" : year.Contains("2017") ? "2017" : year.Contains("2015") ? "2015" : null;
            string joinUrl = !string.IsNullOrEmpty(yearFlag)
                ? $"{BaseUrl}/game/PlaceLauncher.ashx?placeid={placeId}&ticket={ticket}&{yearFlag}=true"
                : $"{BaseUrl}/game/PlaceLauncher.ashx?placeid={placeId}&ticket={ticket}";

            string args = $"-a \"{BaseUrl.Replace("https://", "http://")}/Login/Negotiate.ashx\" -j \"{joinUrl.Replace("https://", "http://")}\" -t \"{ticket}\"";

#if DEBUG
            if (yearFlag != "2015")
            {
                try
                {
                    using (var fs = new FileStream(ClientExe, FileMode.Open, FileAccess.ReadWrite))
                    {
                        var br = new BinaryReader(fs);
                        fs.Position = 0x3C;
                        var peOffset = br.ReadInt32();
                        fs.Position = peOffset + 0x5C;
                        var bw = new BinaryWriter(fs);
                        bw.Write((short)3); // Console subsystem
                    }
                }
                catch { }
            }
#else
            if (yearFlag != "2015")
            {
                try
                {
                    using (var fs = new FileStream(ClientExe, FileMode.Open, FileAccess.ReadWrite))
                    {
                        var br = new BinaryReader(fs);
                        fs.Position = 0x3C;
                        var peOffset = br.ReadInt32();
                        fs.Position = peOffset + 0x5C;
                        short subsystem = br.ReadInt16();
                        if (subsystem == 3)
                        {
                            fs.Position = peOffset + 0x5C;
                            var bw = new BinaryWriter(fs);
                            bw.Write((short)2);
                        }
                    }
                }
                catch { }
            }
#endif

            Process.Start(new ProcessStartInfo
            {
                FileName = ClientExe,
                Arguments = args,
                WorkingDirectory = CurrentYearFolder
            });
            
            DiscordManager.Initialize(placeId, year, ticket);

            this.BeginInvoke((MethodInvoker)delegate { this.Hide(); });

            new Thread(() => {
                Thread.Sleep(5000);
                while (Process.GetProcessesByName("SkybloxPlayerBeta").Length > 0) Thread.Sleep(3000);
                DiscordManager.Shutdown();
                Application.Exit();
            }).Start();
        }

        private void InitializeComponent()
        {
            this.ClientSize = new Size(440, 280);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            this.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0); } };

            var logo = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(180, 90), Top = 30, Left = 130, Cursor = Cursors.Hand };
            logo.Click += (s, e) => { isDarkMode = !isDarkMode; ApplyTheme(); };

            try
            {
                using (var ms = new MemoryStream(Properties.Resources.Skyblox_logo))
                {
                    logo.Image = Image.FromStream(ms);
                }
            }
            catch { }

            status = new Label { Top = 135, Left = 0, Width = 440, Height = 45, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI Semibold", 12f) };
            progress = new ProgressBar { Top = 185, Left = 70, Width = 300, Height = 6 };
            closeBtn = new Label { Text = "✕", Top = 10, Left = 405, Width = 25, Height = 25, Font = new Font("Segoe UI", 12f, FontStyle.Bold), Cursor = Cursors.Hand, TextAlign = ContentAlignment.MiddleCenter };
            closeBtn.Click += (s, e) => Application.Exit();

            repairLink = new Label
            {
                Name = "repairLink",
                Text = "Repair",
                Top = 245, Left = 0, Width = 440,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 8f, FontStyle.Underline),
                Cursor = Cursors.Hand,
                ForeColor = Color.CornflowerBlue
            };
            repairLink.Click += (s, e) =>
            {
                isRepairMode = true;
                Task.Run(StartLauncher);
            };

            var footer = new Label { Name = "footer", Text = $"v{CurrentVersion}", Top = 258, Left = 0, Width = 440, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8f) };

            this.Controls.AddRange(new Control[] { logo, status, progress, closeBtn, repairLink, footer });
            SyncWithWindowsTheme();
        }

        private void ApplyTheme()
        {
            Color bg   = isDarkMode ? Color.FromArgb(25, 25, 25) : Color.White;
            Color text = isDarkMode ? Color.WhiteSmoke : Color.FromArgb(40, 40, 40);
            this.BackColor = bg;
            status.ForeColor = text;
            closeBtn.ForeColor = text;
            foreach (Control c in Controls) if (c.Name == "footer") c.ForeColor = Color.Gray;
        }

        private void SyncWithWindowsTheme()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    isDarkMode = (int)k.GetValue("AppsUseLightTheme") == 0;
            }
            catch { isDarkMode = false; }
            ApplyTheme();
        }

        private void UpdateStatus(string t) => BeginInvoke((MethodInvoker)(() => status.Text = t));
        private void RegisterProtocol() { try { var k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\sclient"); k.SetValue("", "URL:Skyblox Protocol"); k.SetValue("URL Protocol", ""); k.CreateSubKey(@"shell\open\command").SetValue("", $"\"{AppExePath}\" \"%1\""); } catch { } }
    }

    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            string p = null, t = null, y = "2016";
            if (args.Length > 0 && args[0].ToLower().StartsWith("sclient://"))
            {
                var q = HttpUtility.ParseQueryString(new Uri(args[0]).Query);
                p = q["place"] ?? q["placeId"]; t = q["ticket"];
                y = (q["2021"] == "true" || q["year"] == "2021") ? "2021" : (q["2020"] == "true" || q["year"] == "2020") ? "2020" : (q["2019"] == "true" || q["year"] == "2019") ? "2019" : (q["2018"] == "true" || q["year"] == "2018") ? "2018" : (q["2017"] == "true" || q["year"] == "2017") ? "2017" : (q["2015"] == "true" || q["year"] == "2015") ? "2015" : "2016";
            }
            Application.Run(new LauncherForm(p, t, y));
        }
    }
}
// fin


