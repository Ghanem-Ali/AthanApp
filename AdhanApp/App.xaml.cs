using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Media;
using System.Windows.Interop;

namespace AdhanApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Force software rendering to prevent UCEERR_RENDERTHREADFAILURE during high GPU load (e.g. gaming)
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

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