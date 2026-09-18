using SPHCaLArrayRMS.Core.Services;
using System.Windows;
using System.Windows.Threading;

namespace SPHCaLArrayRMS.App
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DiagnosticLogger.Start();
            DispatcherUnhandledException += OnDispatcherUnhandledException;
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            DiagnosticLogger.Log($"Unhandled exception: {e.Exception.Message}");
            MessageBox.Show(e.Exception.Message, "SPH CaL Array RMS", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            DiagnosticLogger.Stop();
            base.OnExit(e);
        }
    }
}
