using System;
using System.Threading;
using System.Windows.Forms;
using JianpuEditor.Rendering;
using JianpuEditor.Services;
using Microsoft.Extensions.DependencyInjection;

namespace JianpuEditor
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            NativeDllLoader.AddNativeDependencyDirectory();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            AppTheme.Load();

            using var services = AppBootstrapper.ConfigureServices();
            Application.Run(services.GetRequiredService<MainForm>());
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            AppLog.Exception("Unhandled exception on UI thread", e.Exception);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            AppLog.Exception("Unhandled application exception", e.ExceptionObject as Exception);
        }
    }
}
