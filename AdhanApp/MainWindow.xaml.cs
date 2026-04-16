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
            CalculateTodayPrayers();
            UpdateUIWithPrayerTimes();

            // استدعاء فوري قبل تشغيل التايمر لضمان دقة الوقت عند الفتح
            UpdateCountdown(DateTime.Now);

            SetupTimer();

            this.Loaded += (s, e) =>
            {
                MoveToSecondaryScreen();
                SetAsBackground();
                SendToBottom();
                setStartup(true); // تفعيل التشغيل مع الويندوز تلقائياً
            };
        }

        private void SetupTrayIcon()
        {
            try
            {
                try
                {
                    var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                    using (Stream stream = assembly.GetManifestResourceStream("AdhanApp.icon.ico"))
                    {
                        if (stream != null)
                            MyNotifyIcon.Icon = new System.Drawing.Icon(stream);
                        else
                            MyNotifyIcon.Icon = System.Drawing.SystemIcons.Shield;
                    }
                }
                catch { MyNotifyIcon.Icon = System.Drawing.SystemIcons.Shield; }
                ContextMenu menu = new ContextMenu();

                MenuItem showItem = new MenuItem { Header = "إظهار / إخفاء النافذة" };
                showItem.Click += Show_Click;

                MenuItem exitItem = new MenuItem { Header = "خروج نهائي" };
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
            // الحساب للمزامنة مع بداية الدقيقة التالية
            int secondsRemaining = 60 - DateTime.Now.Second;
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(secondsRemaining) };
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            // إذا كانت هذه "التكة" الأولى، نغير الفاصل الزمني إلى دقيقة كاملة
            if (timer.Interval.TotalSeconds != 60)
            {
                timer.Interval = TimeSpan.FromMinutes(1);
            }

            DateTime now = DateTime.Now;
            UpdateCountdown(now);
            SendToBottom();

            CheckAndNotify(prayerTimes.Fajr.ToLocalTime(), "الفجر", now);
            CheckAndNotify(prayerTimes.Dhuhr.ToLocalTime(), "الظهر", now);
            CheckAndNotify(prayerTimes.Asr.ToLocalTime(), "العصر", now);
            CheckAndNotify(prayerTimes.Maghrib.ToLocalTime(), "المغرب", now);
            CheckAndNotify(prayerTimes.Isha.ToLocalTime(), "العشاء", now);

            if (now.Hour == 0 && now.Minute == 0)
            {
                CalculateTodayPrayers();
                UpdateUIWithPrayerTimes();
            }
        }

        private void CheckAndNotify(DateTime prayerTime, string prayerName, DateTime now)
        {
            // التحقق من الساعة والدقيقة فقط
            if (now.Hour == prayerTime.Hour && now.Minute == prayerTime.Minute)
            {
                PlayAdhanSound();
                if (notificationsEnabled)
                    try { new ToastContentBuilder().AddText("تنبيه الأذان").AddText($"حان الآن موعد أذان {prayerName}").Show(); } catch { }
            }
        }

        private void PlayAdhanSound()
        {
            if (isMuted) return;
            try
            {
                string soundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "azan_tone.mp3");
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
                {"الفجر", prayerTimes.Fajr.ToLocalTime()}, {"الظهر", prayerTimes.Dhuhr.ToLocalTime()},
                {"العصر", prayerTimes.Asr.ToLocalTime()}, {"المغرب", prayerTimes.Maghrib.ToLocalTime()},
                {"العشاء", prayerTimes.Isha.ToLocalTime()}
            };

            var previous = prayers.Where(p => p.Value <= now).OrderByDescending(p => p.Value).FirstOrDefault();
            var next = prayers.Where(p => p.Value > now).OrderBy(p => p.Value).FirstOrDefault();

            // معالجة حالة ما بعد العشاء للبحث عن فجر الغد
            if (next.Key == null)
            {
                var tomorrow = new PrayerTimes(new Coordinates(lat, lng), DateTime.Today.AddDays(1), CalculationMethod.UmmAlQura());
                next = new KeyValuePair<string, DateTime>("الفجر", tomorrow.Fajr.ToLocalTime());
            }
            if (previous.Key == null)
            {
                var yesterday = new PrayerTimes(new Coordinates(lat, lng), DateTime.Today.AddDays(-1), CalculationMethod.UmmAlQura());
                previous = new KeyValuePair<string, DateTime>("العشاء", yesterday.Isha.ToLocalTime());
            }

            TimeSpan timeSinceLast = now - previous.Value;

            if (timeSinceLast.TotalMinutes > 0 && timeSinceLast.TotalMinutes <= 30)
            {
                lblCountdown.Foreground = System.Windows.Media.Brushes.Red;
                // إزالة الثواني من التنسيق
                lblCountdown.Text = string.Format("-{0}:{1:mm}", (int)timeSinceLast.TotalHours, timeSinceLast);
                UpdateNextPrayerHighlight(previous.Key);
            }
            else
            {
                lblCountdown.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 215, 0));
                TimeSpan timeUntilNext = next.Value - now;
                // إزالة الثواني من التنسيق
                lblCountdown.Text = string.Format("{0}:{1:mm}", (int)timeUntilNext.TotalHours, timeUntilNext);
                UpdateNextPrayerHighlight(next.Key);
            }
        }

        private void UpdateNextPrayerHighlight(string prayerName)
        {
            ResetAllPrayerHighlights();
            System.Windows.Media.SolidColorBrush highlight = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 255, 215, 0));

            if (prayerName == "الفجر") borderFajr.Background = borderFajrTime.Background = highlight;
            else if (prayerName == "الظهر") borderDhuhr.Background = borderDhuhrTime.Background = highlight;
            else if (prayerName == "العصر") borderAsr.Background = borderAsrTime.Background = highlight;
            else if (prayerName == "المغرب") borderMaghrib.Background = borderMaghribTime.Background = highlight;
            else if (prayerName == "العشاء") borderIsha.Background = borderIshaTime.Background = highlight;
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
            // نضع النافذة بجانب زر الإعدادات تقريباً أو في منتصف الشاشة، لكن سنحافظ على نفس المنطق السابق
            var settings = new SettingsWindow(lat, lng, notificationsEnabled, new System.Windows.Point(screenPos.X + this.Width - 50, screenPos.Y + 50));
            settings.Owner = this;
            if (settings.ShowDialog() == true)
            {
                lat = settings.Latitude;
                lng = settings.Longitude;
                notificationsEnabled = settings.NotificationsEnabled;
                CalculateTodayPrayers();
                UpdateUIWithPrayerTimes();
                UpdateCountdown(DateTime.Now);
            }
        }
    }
}