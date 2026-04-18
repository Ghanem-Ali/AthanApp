using System.Configuration;
using System.Data;
using System.Windows;

namespace AdhanApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            this.DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show($"خطأ في التشغيل:\n\n{args.Exception.Message}\n\n{args.Exception.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
                System.Windows.Application.Current.Shutdown();
            };
            base.OnStartup(e);
        }
    }
}