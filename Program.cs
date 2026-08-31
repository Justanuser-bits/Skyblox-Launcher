// im tired

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
        private const string CurrentVersion = "1.0.2";
#endif
        private const string VersionUrl = "https://skyblox.co/clients/version.txt";
        private const string LauncherDownloadUrl = "https://skyblox.co/clients/SkybloxLauncher.exe";

        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;
        [DllImport("user32.dll")] public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")] public static extern bool ReleaseCapture();

        private readonly string placeId, ticket, year;
        private readonly string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Skyblox");

        private string CurrentYearFolder => Path.Combine(appData, year.Contains("2021") ? "2021" : year.Contains("2020") ? "2020" : year.Contains("2015") ? "2015" : "2016");
        private string ClientExe => Path.Combine(CurrentYearFolder, "SkybloxPlayerBeta.exe");
        private string AppExePath => Application.ExecutablePath;
        
        private ProgressBar progress;
        private Label status, closeBtn;
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

                // previous problem: we didnt install 2020 and 2021 only 2016 so use InstallAllMissingClients to ensure we have our clients
                await InstallAllMissingClients();

                if (!string.IsNullOrEmpty(placeId))
                    LaunchGame();
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
                    string latest = (await client.GetStringAsync($"{VersionUrl}?t={DateTime.Now.Ticks}")).Trim();
                    if (CurrentVersion != "DEBUG" && latest != CurrentVersion)
                    {
                        UpdateStatus($"Updating to v{latest}...");
                        byte[] newExe = await client.GetByteArrayAsync(LauncherDownloadUrl);
                        string tmpPath = AppExePath + ".tmp";
                        File.WriteAllBytes(tmpPath, newExe);

                        string batch = $"@echo off\ntimeout /t 1\ndel \"{AppExePath}\"\nmove \"{tmpPath}\" \"{AppExePath}\"\nstart \"\" \"{AppExePath}\"\nexit";
                        File.WriteAllText("update.bat", batch);
                        Process.Start(new ProcessStartInfo("update.bat") { CreateNoWindow = true, UseShellExecute = false });
                        Application.Exit();
                    }
                }
            }
            catch { }
        }

        private async Task InstallAllMissingClients()
        {
            string[] years = { "2015", "2016", "2020" };
            string[] urls = {
                "http://skyblox.co/clients/15client.zip",
                "http://skyblox.co/clients/16client.zip",
                "http://skyblox.co/clients/20client.zip"
            };

            for (int i = 0; i < years.Length; i++)
            {
                if (!this.year.Contains(years[i]) && !isRepairMode) continue;

                string path = Path.Combine(appData, years[i]);
                string exeName = "SkybloxPlayerBeta.exe";
                string exePath = Path.Combine(path, exeName);

                if (!File.Exists(exePath) || (isRepairMode && year.Contains(years[i])))
                {
                    bool isUpdate = File.Exists(exePath);
                    string actionStr = isUpdate ? "Updating" : "Downloading";
                    UpdateStatus($"{actionStr} {years[i]}...");
                    string zip = Path.Combine(appData, "temp.zip");
                    await DownloadFile(urls[i], zip, years[i], actionStr);

                    UpdateStatus($"Extracting {years[i]}...");
                    if (Directory.Exists(path)) Directory.Delete(path, true);
                    ZipFile.ExtractToDirectory(zip, path);
                    File.Delete(zip);
                }
            }
        }

        private async Task DownloadFile(string url, string dest, string yearLabel, string actionStr)
        {
            using (var client = new HttpClient())
            using (var res = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
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
                MessageBox.Show($"Missing: {ClientExe}\nHold Shift while opening to Repair.");
                return;
            }

            string yearFlag = year.Contains("2021") ? "2021" : year.Contains("2020") ? "2020" : year.Contains("2015") ? "2015" : null;
            string joinUrl = !string.IsNullOrEmpty(yearFlag)
                ? $"http://skyblox.co/game/PlaceLauncher.ashx?placeid={placeId}&ticket={ticket}&{yearFlag}=true"
                : $"http://skyblox.co/game/PlaceLauncher.ashx?placeid={placeId}&ticket={ticket}";


#if DEBUG
            string args = $"-console -a \"http://skyblox.co/Login/Negotiate.ashx\" -j \"{joinUrl}\" -t \"{ticket}\"";
#else
            string args = $"-a \"http://skyblox.co/Login/Negotiate.ashx\" -j \"{joinUrl}\" -t \"{ticket}\"";
#endif

            if (yearFlag == "2015")
            {
                string scriptUrl = $"http://skyblox.co/game/Join.ashx?placeid={placeId}&ticket={ticket}";
                args = $"--play -a \"http://skyblox.co/Login/Negotiate.ashx\" -j \"{scriptUrl}\" -t \"{ticket}\"";
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = ClientExe,
                Arguments = args,
                WorkingDirectory = CurrentYearFolder
            });

            this.BeginInvoke((MethodInvoker)delegate { this.Hide(); });

            new Thread(() => {
                Thread.Sleep(5000);
                while (Process.GetProcessesByName("SkybloxPlayerBeta").Length > 0) Thread.Sleep(3000);
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

            var footer = new Label { Name = "footer", Text = $"Hold SHIFT to repair | v{CurrentVersion}", Top = 245, Left = 0, Width = 440, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8f) };

            var vers = new Label { Name = "vers", Text = $"v{CurrentVersion}", Top = 245, Left = 0, Width = 440, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8f) };

            this.Controls.AddRange(new Control[] { logo, status, progress, closeBtn, footer });
            SyncWithWindowsTheme();
        }

        private void ApplyTheme()
        {
            Color bg = isDarkMode ? Color.FromArgb(25, 25, 25) : Color.White;
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
                y = (q["2021"] == "true" || q["year"] == "2021") ? "2021" : (q["2020"] == "true" || q["year"] == "2020") ? "2020" : (q["2015"] == "true" || q["year"] == "2015") ? "2015" : "2016";
            }
            Application.Run(new LauncherForm(p, t, y));
        }
    }
}
// fin
