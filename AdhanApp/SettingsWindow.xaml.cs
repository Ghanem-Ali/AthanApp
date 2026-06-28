using System.Windows;
using System.Windows.Input;
using System;
using System.Collections.Generic;
using Batoulapps.Adhan;

namespace AdhanApp
{
    public partial class SettingsWindow : Window
    {
        public double Latitude { get; private set; }
        public double Longitude { get; private set; }
        public bool NotificationsEnabled { get; private set; }
        public int ScreenIndex { get; private set; }
        public string WindowPosition { get; private set; }
        public double Volume { get; private set; }
        public CalculationMethod CalcMethod { get; private set; }
        private bool _saved = false;

        private readonly Dictionary<CalculationMethod, string> _calcMethodsArabic = new Dictionary<CalculationMethod, string>
        {
            { CalculationMethod.MUSLIM_WORLD_LEAGUE, "رابطة العالم الإسلامي" },
            { CalculationMethod.EGYPTIAN, "الهيئة المصرية العامة للمساحة" },
            { CalculationMethod.KARACHI, "جامعة العلوم الإسلامية بكراتشي" },
            { CalculationMethod.UMM_AL_QURA, "جامعة أم القرى بمكة المكرمة" },
            { CalculationMethod.DUBAI, "دبي" },
            { CalculationMethod.MOON_SIGHTING_COMMITTEE, "لجنة تقصي الأهلة" },
            { CalculationMethod.NORTH_AMERICA, "الجمعية الإسلامية لأمريكا الشمالية" },
            { CalculationMethod.KUWAIT, "الكويت" },
            { CalculationMethod.QATAR, "قطر" },
            { CalculationMethod.SINGAPORE, "سنغافورة" },
            { CalculationMethod.OTHER, "أخرى" }
        };

        public SettingsWindow(double currentLat, double currentLng, bool notificationsEnabled, int screenIndex, string windowPosition, double volume, CalculationMethod calcMethod, System.Windows.Point mousePos)
        {
            InitializeComponent();
            Latitude = currentLat;
            Longitude = currentLng;
            NotificationsEnabled = notificationsEnabled;
            ScreenIndex = screenIndex;
            WindowPosition = windowPosition;
            Volume = volume;
            CalcMethod = calcMethod;

            txtLat.Text = currentLat.ToString();
            txtLng.Text = currentLng.ToString();
            toggleNotifications.IsChecked = notificationsEnabled;
            sliderVolume.Value = volume;

            // Populate screens
            var screens = ScreenHelper.AllScreens();
            for (int i = 0; i < screens.Count; i++)
            {
                comboScreen.Items.Add($"شاشة {i + 1}" + (screens[i].Primary ? " (الرئيسية)" : ""));
            }
            comboScreen.SelectedIndex = (screenIndex >= 0 && screenIndex < screens.Count) ? screenIndex : 0;

            foreach (var method in _calcMethodsArabic)
            {
                comboCalcMethod.Items.Add(method.Value);
            }
            comboCalcMethod.SelectedItem = _calcMethodsArabic.ContainsKey(calcMethod) ? _calcMethodsArabic[calcMethod] : _calcMethodsArabic[CalculationMethod.UMM_AL_QURA];

            UpdatePositionUI();

            // Position window at mouse click location
            this.Left = mousePos.X;
            this.Top = mousePos.Y;
        }

        private void UpdatePositionUI()
        {
            foreach (var child in gridPositionSelector.Children)
            {
                if (child is System.Windows.Shapes.Rectangle rect)
                {
                    rect.Fill = rect.Tag.ToString() == WindowPosition 
                        ? System.Windows.Media.Brushes.Gold 
                        : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(85, 85, 85));
                }
            }
        }

        private void Pos_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Shapes.Rectangle rect)
            {
                WindowPosition = rect.Tag.ToString() ?? "TopLeft";
                UpdatePositionUI();
                TrySave();
            }
        }

        private void TrySave()
        {
            if (double.TryParse(txtLat.Text, out double lat) &&
                double.TryParse(txtLng.Text, out double lng) &&
                lat >= -90 && lat <= 90 && lng >= -180 && lng <= 180)
            {
                Latitude = lat;
                Longitude = lng;
                NotificationsEnabled = toggleNotifications.IsChecked == true;
                ScreenIndex = comboScreen.SelectedIndex;
                Volume = sliderVolume.Value;
                
                var selectedArabicName = comboCalcMethod.SelectedItem?.ToString();
                foreach (var pair in _calcMethodsArabic)
                {
                    if (pair.Value == selectedArabicName)
                    {
                        CalcMethod = pair.Key;
                        break;
                    }
                }
                
                _saved = true;
            }
        }

        private void OnValueChanged(object sender, RoutedEventArgs e) => TrySave();

        private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter) TrySave();
        }

        private void OnToggleChanged(object sender, RoutedEventArgs e) => TrySave();

        private void BtnTestVolume_Click(object sender, RoutedEventArgs e)
        {
            if (this.Owner is MainWindow main)
            {
                main.TestSound(sliderVolume.Value);
            }
        }

        private void Window_Deactivated(object? sender, EventArgs e)
        {
            TrySave();
            DialogResult = _saved;
            Close();
        }
    }
}
