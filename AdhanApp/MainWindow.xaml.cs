using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;
using Microsoft.Win32;
using AzanDotNet;
using Microsoft.Toolkit.Uwp.Notifications;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Globalization;

namespace AdhanApp
{
    public partial class MainWindow : Window
    {
        // --- Win32 API ---
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);
        [DllImport("user32.dll")] static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)] static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOACTIVATE = 0x0010;

        // --- Variables ---
        private DispatcherTimer timer = default!;
        private PrayerTimes prayerTimes = default!;
        private MediaPlayer mediaPlayer = new MediaPlayer();
        private bool isMuted = false;
        private bool notificationsEnabled = true;
        double lat = 18.3000;
        double lng = 42.7333;

        public MainWindow()
        {
            InitializeComponent();

            this.ShowInTaskbar = false;
            this.Topmost = false;

            SetupTrayIcon();
            LoadSettings(); // طھط­ظ…ظٹظ„ ط§ظ„ط¥ط¹ط¯ط§ط¯ط§طھ ط§ظ„ظ…ط­ظپظˆط¸ط©
            CalculateTodayPrayers();
            UpdateUIWithPrayerTimes();

            // ط§ط³طھط¯ط¹ط§ط، ظپظˆط±ظٹ ظ‚ط¨ظ„ طھط´ط؛ظٹظ„ ط§ظ„طھط§ظٹظ…ط± ظ„ط¶ظ…ط§ظ† ط¯ظ‚ط© ط§ظ„ظˆظ‚طھ ط¹ظ†ط¯ ط§ظ„ظپطھط­
            UpdateCountdown(DateTime.Now);

            SetupTimer();

            this.Loaded += (s, e) =>
            {
                MoveToSecondaryScreen();
                SetAsBackground();
                SendToBottom();
                setStartup(true); // طھظپط¹ظٹظ„ ط§ظ„طھط´ط؛ظٹظ„ ظ…ط¹ ط§ظ„ظˆظٹظ†ط¯ظˆط² طھظ„ظ‚ط§ط¦ظٹط§ظ‹
            };
        }

        private void SetupTrayIcon()
        {
            try
            {
                try
                {
                    var uri = new Uri("pack://application:,,,/icon.ico");
                    var streamInfo = System.Windows.Application.GetResourceStream(uri);
                    if (streamInfo != null)
                        MyNotifyIcon.Icon = new System.Drawing.Icon(streamInfo.Stream);
                    else
                        MyNotifyIcon.Icon = System.Drawing.SystemIcons.Shield;
                }
                catch { MyNotifyIcon.Icon = System.Drawing.SystemIcons.Shield; }

                ContextMenu menu = new ContextMenu();

                MenuItem showItem = new MenuItem { Header = "ط¥ط¸ظ‡ط§ط± / ط¥ط®ظپط§ط، ط§ظ„ظ†ط§ظپط°ط©" };
                showItem.Click += Show_Click;

                MenuItem exitItem = new MenuItem { Header = "ط®ط±ظˆط¬ ظ†ظ‡ط§ط¦ظٹ" };
                exitItem.Click += Exit_Click;

                menu.Items.Add(showItem);
                menu.Items.Add(new Separator());
                menu.Items.Add(exitItem);

                MyNotifyIcon.ContextMenu = menu;
            }
            catch { }
        }

        private void SetAsBackground()
        {
            IntPtr windowHandle = new WindowInteropHelper(this).Handle;
            IntPtr progman = FindWindow("Progman", null);
            SendMessage(progman, 0x052C, new IntPtr(0), IntPtr.Zero);

            IntPtr workerw = IntPtr.Zero;
            EnumWindows(new EnumWindowsProc((tophandle, topparamhandle) =>
            {
                IntPtr p = FindWindowEx(tophandle, IntPtr.Zero, "SHELLDLL_DefView", "");
                if (p != IntPtr.Zero)
                    workerw = FindWindowEx(IntPtr.Zero, tophandle, "WorkerW", "");
                return true;
            }), IntPtr.Zero);

            if (workerw != IntPtr.Zero) SetParent(windowHandle, workerw);
        }

        private void SendToBottom()
        {
            IntPtr windowHandle = new WindowInteropHelper(this).Handle;
            SetWindowPos(windowHandle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        private void MoveToSecondaryScreen()
        {
            try
            {
                var screens = System.Windows.Forms.Screen.AllScreens;
                if (screens.Length > 1)
                {
                    var target = screens.FirstOrDefault(s => !s.Primary) ?? screens[0];
                    this.Left = target.WorkingArea.Left;
                    this.Top = target.WorkingArea.Top;
                }
                else { this.Left = 0; this.Top = 0; }
            }
            catch { this.Left = 0; this.Top = 0; }
        }

        private void SetupTimer()
        {
            // ط§ظ„ط­ط³ط§ط¨ ظ„ظ„ظ…ط²ط§ظ…ظ†ط© ظ…ط¹ ط¨ط¯ط§ظٹط© ط§ظ„ط¯ظ‚ظٹظ‚ط© ط§ظ„طھط§ظ„ظٹط©
            int secondsRemaining = 60 - DateTime.Now.Second;
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(secondsRemaining) };
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            // ط¥ط°ط§ ظƒط§ظ†طھ ظ‡ط°ظ‡ "ط§ظ„طھظƒط©" ط§ظ„ط£ظˆظ„ظ‰طŒ ظ†ط؛ظٹط± ط§ظ„ظپط§طµظ„ ط§ظ„ط²ظ…ظ†ظٹ ط¥ظ„ظ‰ ط¯ظ‚ظٹظ‚ط© ظƒط§ظ…ظ„ط©
            if (timer.Interval.TotalSeconds != 60)
            {
                timer.Interval = TimeSpan.FromMinutes(1);
            }

            DateTime now = DateTime.Now;
            UpdateCountdown(now);
            SendToBottom();

            CheckAndNotify(prayerTimes.Fajr.ToLocalTime(), "ط§ظ„ظپط¬ط±", now);
            CheckAndNotify(prayerTimes.Dhuhr.ToLocalTime(), "ط§ظ„ط¸ظ‡ط±", now);
            CheckAndNotify(prayerTimes.Asr.ToLocalTime(), "ط§ظ„ط¹طµط±", now);
            CheckAndNotify(prayerTimes.Maghrib.ToLocalTime(), "ط§ظ„ظ…ط؛ط±ط¨", now);
            CheckAndNotify(prayerTimes.Isha.ToLocalTime(), "ط§ظ„ط¹ط´ط§ط،", now);

            if (now.Hour == 0 && now.Minute == 0)
            {
                CalculateTodayPrayers();
                UpdateUIWithPrayerTimes();
            }
        }

        private void CheckAndNotify(DateTime prayerTime, string prayerName, DateTime now)
        {
            // ط§ظ„طھط­ظ‚ظ‚ ظ…ظ† ط§ظ„ط³ط§ط¹ط© ظˆط§ظ„ط¯ظ‚ظٹظ‚ط© ظپظ‚ط·
            if (now.Hour == prayerTime.Hour && now.Minute == prayerTime.Minute)
            {
                PlayAdhanSound();
                if (notificationsEnabled)
                    try { new ToastContentBuilder().AddText("طھظ†ط¨ظٹظ‡ ط§ظ„ط£ط°ط§ظ†").AddText($"ط­ط§ظ† ط§ظ„ط¢ظ† ظ…ظˆط¹ط¯ ط£ط°ط§ظ† {prayerName}").Show(); } catch { }
            }
        }

        private void PlayAdhanSound()
        {
            if (isMuted) return;
            try
            {
                string soundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "azan_tone.mp3");
                
                // If not found in local folder, extract from resources to Temp
                if (!File.Exists(soundPath))
                {
                    soundPath = Path.Combine(Path.GetTempPath(), "Athan_azan_tone.mp3");
                    if (!File.Exists(soundPath))
                    {
                        try
                        {
                            var uri = new Uri("pack://application:,,,/azan_tone.mp3");
                            var streamInfo = System.Windows.Application.GetResourceStream(uri);
                            if (streamInfo != null)
                            {
                                using (var fs = new FileStream(soundPath, FileMode.Create))
                                {
                                    streamInfo.Stream.CopyTo(fs);
                                }
                            }
                        }
                        catch { }
                    }
                }

                if (File.Exists(soundPath))
                {
                    mediaPlayer.Open(new Uri(soundPath));
                    mediaPlayer.Volume = 1.0;
                    mediaPlayer.Play();
                }
            }
            catch { }
        }

        private void UpdateCountdown(DateTime now)
        {
            var prayers = new Dictionary<string, DateTime> {
                {"ط§ظ„ظپط¬ط±", prayerTimes.Fajr.ToLocalTime()}, {"ط§ظ„ط¸ظ‡ط±", prayerTimes.Dhuhr.ToLocalTime()},
                {"ط§ظ„ط¹طµط±", prayerTimes.Asr.ToLocalTime()}, {"ط§ظ„ظ…ط؛ط±ط¨", prayerTimes.Maghrib.ToLocalTime()},
                {"ط§ظ„ط¹ط´ط§ط،", prayerTimes.Isha.ToLocalTime()}
            };

            var previous = prayers.Where(p => p.Value <= now).OrderByDescending(p => p.Value).FirstOrDefault();
            var next = prayers.Where(p => p.Value > now).OrderBy(p => p.Value).FirstOrDefault();

            // ظ…ط¹ط§ظ„ط¬ط© ط­ط§ظ„ط© ظ…ط§ ط¨ط¹ط¯ ط§ظ„ط¹ط´ط§ط، ظ„ظ„ط¨ط­ط« ط¹ظ† ظپط¬ط± ط§ظ„ط؛ط¯
            if (next.Key == null)
            {
                var tomorrow = new PrayerTimes(new Coordinates(lat, lng), DateTime.Today.AddDays(1), CalculationMethod.UmmAlQura());
                next = new KeyValuePair<string, DateTime>("ط§ظ„ظپط¬ط±", tomorrow.Fajr.ToLocalTime());
            }
            if (previous.Key == null)
            {
                var yesterday = new PrayerTimes(new Coordinates(lat, lng), DateTime.Today.AddDays(-1), CalculationMethod.UmmAlQura());
                previous = new KeyValuePair<string, DateTime>("ط§ظ„ط¹ط´ط§ط،", yesterday.Isha.ToLocalTime());
            }

            TimeSpan timeSinceLast = now - previous.Value;

            if (timeSinceLast.TotalMinutes > 0 && timeSinceLast.TotalMinutes <= 30)
            {
                lblCountdown.Foreground = System.Windows.Media.Brushes.Red;
                // ط¥ط²ط§ظ„ط© ط§ظ„ط«ظˆط§ظ†ظٹ ظ…ظ† ط§ظ„طھظ†ط³ظٹظ‚
                lblCountdown.Text = string.Format("-{0}:{1:mm}", (int)timeSinceLast.TotalHours, timeSinceLast);
                UpdateNextPrayerHighlight(previous.Key);
            }
            else
            {
                lblCountdown.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 215, 0));
                TimeSpan timeUntilNext = next.Value - now;
                // ط¥ط²ط§ظ„ط© ط§ظ„ط«ظˆط§ظ†ظٹ ظ…ظ† ط§ظ„طھظ†ط³ظٹظ‚
                lblCountdown.Text = string.Format("{0}:{1:mm}", (int)timeUntilNext.TotalHours, timeUntilNext);
                UpdateNextPrayerHighlight(next.Key);
            }
        }

        private void UpdateNextPrayerHighlight(string prayerName)
        {
            ResetAllPrayerHighlights();
            System.Windows.Media.SolidColorBrush highlight = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 255, 215, 0));

            if (prayerName == "ط§ظ„ظپط¬ط±") borderFajr.Background = borderFajrTime.Background = highlight;
            else if (prayerName == "ط§ظ„ط¸ظ‡ط±") borderDhuhr.Background = borderDhuhrTime.Background = highlight;
            else if (prayerName == "ط§ظ„ط¹طµط±") borderAsr.Background = borderAsrTime.Background = highlight;
            else if (prayerName == "ط§ظ„ظ…ط؛ط±ط¨") borderMaghrib.Background = borderMaghribTime.Background = highlight;
            else if (prayerName == "ط§ظ„ط¹ط´ط§ط،") borderIsha.Background = borderIshaTime.Background = highlight;
        }

        private void ResetAllPrayerHighlights()
        {
            foreach (var child in gridPrayers.Children)
                if (child is Border b) b.Background = System.Windows.Media.Brushes.Transparent;
        }

        private void CalculateTodayPrayers() => prayerTimes = new PrayerTimes(new Coordinates(lat, lng), DateTime.Today, CalculationMethod.UmmAlQura());

        private void UpdateUIWithPrayerTimes()
        {
            txtFajr.Text = prayerTimes.Fajr.ToLocalTime().ToString("hh:mm tt");
            txtDhuhr.Text = prayerTimes.Dhuhr.ToLocalTime().ToString("hh:mm tt");
            txtAsr.Text = prayerTimes.Asr.ToLocalTime().ToString("hh:mm tt");
            txtMaghrib.Text = prayerTimes.Maghrib.ToLocalTime().ToString("hh:mm tt");
            txtIsha.Text = prayerTimes.Isha.ToLocalTime().ToString("hh:mm tt");
        }

        private void setStartup(bool enable)
        {
            try
            {
                using (RegistryKey? rk = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (rk != null)
                    {
                        string? path = Environment.ProcessPath;
                        if (string.IsNullOrEmpty(path)) return;

                        if (enable) rk.SetValue("AdhanWidgetApp", path);
                        else rk.DeleteValue("AdhanWidgetApp", false);
                    }
                }
            }
            catch { }
        }

        private bool CheckStartupStatus()
        {
            try
            {
                using (RegistryKey? rk = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    return rk?.GetValue("AdhanWidgetApp") != null;
                }
            }
            catch { return false; }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }

        private void MyNotifyIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            if (this.IsVisible) this.Hide();
            else { this.Show(); this.Activate(); }
        }

        private void Show_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsVisible) this.Hide();
            else { this.Show(); this.Activate(); }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) this.DragMove();
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            var screenPos = PointToScreen(new System.Windows.Point(0, 0));
            // ظ†ط¶ط¹ ط§ظ„ظ†ط§ظپط°ط© ط¨ط¬ط§ظ†ط¨ ط²ط± ط§ظ„ط¥ط¹ط¯ط§ط¯ط§طھ طھظ‚ط±ظٹط¨ط§ظ‹ ط£ظˆ ظپظٹ ظ…ظ†طھطµظپ ط§ظ„ط´ط§ط´ط©طŒ ظ„ظƒظ† ط³ظ†ط­ط§ظپط¸ ط¹ظ„ظ‰ ظ†ظپط³ ط§ظ„ظ…ظ†ط·ظ‚ ط§ظ„ط³ط§ط¨ظ‚
            var settings = new SettingsWindow(lat, lng, notificationsEnabled, new System.Windows.Point(screenPos.X + this.Width - 50, screenPos.Y + 50));
            settings.Owner = this;
            if (settings.ShowDialog() == true)
            {
                lat = settings.Latitude;
                lng = settings.Longitude;
                notificationsEnabled = settings.NotificationsEnabled;
                SaveSettings(); // ط­ظپط¸ ط§ظ„ط¥ط¹ط¯ط§ط¯ط§طھ ط§ظ„ط¬ط¯ظٹط¯ط©
                CalculateTodayPrayers();
                UpdateUIWithPrayerTimes();
                UpdateCountdown(DateTime.Now);
            }
        }

        private void SaveSettings()
        {
            try
            {
                using (RegistryKey? rk = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\AdhanApp"))
                {
                    rk?.SetValue("Latitude", lat.ToString(CultureInfo.InvariantCulture));
                    rk?.SetValue("Longitude", lng.ToString(CultureInfo.InvariantCulture));
                    rk?.SetValue("NotificationsEnabled", notificationsEnabled.ToString());
                }
            }
            catch { }
        }

        private void LoadSettings()
        {
            try
            {
                using (RegistryKey? rk = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\AdhanApp"))
                {
                    if (rk != null)
                    {
                        if (double.TryParse(rk.GetValue("Latitude")?.ToString(), CultureInfo.InvariantCulture, out double l)) lat = l;
                        if (double.TryParse(rk.GetValue("Longitude")?.ToString(), CultureInfo.InvariantCulture, out double lo)) lng = lo;
                        if (bool.TryParse(rk.GetValue("NotificationsEnabled")?.ToString(), out bool n)) notificationsEnabled = n;
                    }
                }
            }
            catch { }
        }
    }
}

