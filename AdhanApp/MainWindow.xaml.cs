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

namespace AdhanApp
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer timer;
        private PrayerTimes prayerTimes;
        private MediaPlayer mediaPlayer = new MediaPlayer();
        // إحداثيات خميس مشيط
        double lat = 18.3000;
        double lng = 42.7333;

        public MainWindow()
        {
            InitializeComponent();
            try
            {
                MyNotifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
            }
            catch { }

            CalculateTodayPrayers();
            UpdateUIWithPrayerTimes();
            SetupTimer();

            // الانتقال للشاشة الثانية فور التحميل
            this.Loaded += (s, e) => MoveToSecondaryScreen();
        }

        private void MoveToSecondaryScreen()
        {
            try
            {
                var screens = System.Windows.Forms.Screen.AllScreens;
                if (screens.Length > 1)
                {
                    // اختيار الشاشة غير الأساسية
                    var target = screens.FirstOrDefault(s => !s.Primary) ?? screens[0];
                    this.Left = target.WorkingArea.Left;
                    this.Top = target.WorkingArea.Top;
                }
                else
                {
                    this.Left = 0;
                    this.Top = 0;
                }
            }
            catch
            {
                this.Left = 0;
                this.Top = 0;
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) this.DragMove();
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
                else
                {
                    // تصحيح الخطأ المطبعي هنا
                    Console.Beep(1000, 500);
                }
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
                try
                {
                    new ToastContentBuilder()
                        .AddText("تنبيه الأذان")
                        .AddText($"حان الآن موعد أذان {prayerName}")
                        .Show();
                }
                catch { }
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            DateTime now = DateTime.Now;
            UpdateCountdown(now);
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
                // التنسيق h:mm:ss سيعرض 2:19:13 بدلاً من 02:19:13
                lblCountdown.Text = string.Format("{0}:{1:mm}:{1:ss}", (int)diff.TotalHours, diff);
                UpdateNextPrayerHighlight(next.Key);
            }
        }

        private void UpdateNextPrayerHighlight(string prayerName)
        {
            ResetAllPrayerHighlights();
            // تمييز الصلاة القادمة بلون ذهبي شفاف
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

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) { e.Cancel = true; this.Hide(); }
        private void MyNotifyIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e) { if (this.IsVisible) this.Hide(); else { this.Show(); this.Activate(); } }
        private void Show_Click(object sender, RoutedEventArgs e) => MyNotifyIcon_TrayMouseDoubleClick(null, null);
        private void Exit_Click(object sender, RoutedEventArgs e) => System.Windows.Application.Current.Shutdown();
    }
}