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

namespace AdhanApp
{
    public partial class MainWindow : Window
    {
        // --- Win32 API (لجعل النافذة خلفية ثابتة) ---
        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll")]
        static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);
        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOACTIVATE = 0x0010;

        // --- Variables ---
        private DispatcherTimer timer;
        private PrayerTimes prayerTimes;
        private MediaPlayer mediaPlayer = new MediaPlayer();
        double lat = 18.3000;
        double lng = 42.7333;

        public MainWindow()
        {
            InitializeComponent();

            // إعدادات النافذة الأساسية
            this.ShowInTaskbar = false;
            this.Topmost = false;

            // إعداد الأيقونة والقائمة المنبثقة (الزر الأيمن)
            SetupTrayIcon();

            CalculateTodayPrayers();
            UpdateUIWithPrayerTimes();
            SetupTimer();

            this.Loaded += (s, e) =>
            {
                MoveToSecondaryScreen();
                SetAsBackground();
                SendToBottom();
            };
        }

        private void SetupTrayIcon()
        {
            try
            {
                // --- سطر الأيقونة المؤقت لضمان الظهور ---
                // نستخدم أيقونة "الدرع" من النظام لنتأكد أنها ستظهر فوراً
                MyNotifyIcon.Icon = System.Drawing.SystemIcons.Shield;

                // إنشاء القائمة التي تظهر بالزر الأيمن
                ContextMenu menu = new ContextMenu();

                MenuItem showItem = new MenuItem { Header = "إظهار / إخفاء النافذة" };
                showItem.Click += Show_Click;

                MenuItem exitItem = new MenuItem { Header = "خروج نهائي" };
                exitItem.Click += Exit_Click;

                menu.Items.Add(showItem);
                menu.Items.Add(new Separator());
                menu.Items.Add(exitItem);

                // ربط القائمة بالأيقونة
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

        private void Timer_Tick(object sender, EventArgs e)
        {
            DateTime now = DateTime.Now;
            UpdateCountdown(now);

            // إجبار النافذة على البقاء في القاع كل ثانية
            SendToBottom();

            CheckAndNotify(prayerTimes.Fajr.ToLocalTime(), "الفجر", now);
            CheckAndNotify(prayerTimes.Dhuhr.ToLocalTime(), "الظهر", now);
            CheckAndNotify(prayerTimes.Asr.ToLocalTime(), "العصر", now);
            CheckAndNotify(prayerTimes.Maghrib.ToLocalTime(), "المغرب", now);
            CheckAndNotify(prayerTimes.Isha.ToLocalTime(), "العشاء", now);

            if (now.Hour == 0 && now.Minute == 0 && now.Second == 0)
            {
                CalculateTodayPrayers();
                UpdateUIWithPrayerTimes();
            }
        }

        private void PlayAdhanSound()
        {
            try
            {
                string soundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "azan_tone.mp3");
                if (File.Exists(soundPath))
                {
                    mediaPlayer.Open(new Uri(soundPath));
                    mediaPlayer.Volume = 1.0;
                    mediaPlayer.Play();
                }
                else { Console.Beep(1000, 500); }
            }
            catch { }
        }

        private void SetupTimer()
        {
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void CheckAndNotify(DateTime prayerTime, string prayerName, DateTime now)
        {
            if (now.Hour == prayerTime.Hour && now.Minute == prayerTime.Minute && now.Second == 0)
            {
                PlayAdhanSound();
                try { new ToastContentBuilder().AddText("تنبيه الأذان").AddText($"حان الآن موعد أذان {prayerName}").Show(); } catch { }
            }
        }

        private void UpdateCountdown(DateTime now)
        {
            var prayers = new Dictionary<string, DateTime> {
                {"الفجر", prayerTimes.Fajr.ToLocalTime()}, {"الظهر", prayerTimes.Dhuhr.ToLocalTime()},
                {"العصر", prayerTimes.Asr.ToLocalTime()}, {"المغرب", prayerTimes.Maghrib.ToLocalTime()},
                {"العشاء", prayerTimes.Isha.ToLocalTime()}
            };
            var next = prayers.Where(p => p.Value > now).OrderBy(p => p.Value).FirstOrDefault();
            if (next.Key != null)
            {
                TimeSpan diff = next.Value - now;
                lblCountdown.Text = string.Format("{0}:{1:mm}:{1:ss}", (int)diff.TotalHours, diff);
                UpdateNextPrayerHighlight(next.Key);
            }
        }

        private void UpdateNextPrayerHighlight(string prayerName)
        {
            ResetAllPrayerHighlights();
            var highlight = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 255, 215, 0));
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

        // --- التعامل مع الأيقونة والقائمة ---
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
            // تم تحديد المسار الكامل لتجنب خطأ التعارض Ambiguous Reference
            System.Windows.Application.Current.Shutdown();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) this.DragMove();
        }
    }
}