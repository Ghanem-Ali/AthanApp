using System.Windows;
using System.Windows.Input;

namespace AdhanApp
{
    public partial class SettingsWindow : Window
    {
        public double Latitude { get; private set; }
        public double Longitude { get; private set; }
        public bool NotificationsEnabled { get; private set; }
        private bool _saved = false;

        public SettingsWindow(double currentLat, double currentLng, bool notificationsEnabled, System.Windows.Point mousePos)
        {
            InitializeComponent();
            Latitude = currentLat;
            Longitude = currentLng;
            NotificationsEnabled = notificationsEnabled;

            txtLat.Text = currentLat.ToString();
            txtLng.Text = currentLng.ToString();
            toggleNotifications.IsChecked = notificationsEnabled;

            // Position window at mouse click location
            this.Left = mousePos.X;
            this.Top = mousePos.Y;
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
                _saved = true;
            }
        }

        private void OnValueChanged(object sender, RoutedEventArgs e) => TrySave();

        private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter) TrySave();
        }

        private void OnToggleChanged(object sender, RoutedEventArgs e) => TrySave();

        private void Window_Deactivated(object? sender, EventArgs e)
        {
            TrySave();
            DialogResult = _saved;
            Close();
        }
    }
}
