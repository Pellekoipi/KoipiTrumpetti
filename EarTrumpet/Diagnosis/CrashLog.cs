using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace EarTrumpet.Diagnosis
{
    static class CrashLog
    {
        private static readonly object s_gate = new object();
        private static bool s_registered;

        public static string LogPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KoipiTrumpetti",
            "crash.log");

        public static void Register(Application app)
        {
            if (s_registered || app == null)
            {
                return;
            }

            s_registered = true;
            app.DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        public static void Write(Exception exception, string source = null)
        {
            try
            {
                var builder = new StringBuilder();
                builder.Append("===== ");
                builder.Append(DateTime.UtcNow.ToString("o"));
                if (!string.IsNullOrEmpty(source))
                {
                    builder.Append(' ');
                    builder.Append(source);
                }
                builder.AppendLine(" =====");
                builder.AppendLine(exception == null ? "(null exception)" : exception.ToString());
                builder.AppendLine();

                lock (s_gate)
                {
                    var path = LogPath;
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.AppendAllText(path, builder.ToString());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"CrashLog write failed {ex}");
            }
        }

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Write(e.Exception, "Dispatcher");
            e.Handled = true;
        }

        private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString() ?? "(null)");
            Write(exception, e.IsTerminating ? "AppDomain terminating" : "AppDomain");
        }

        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Write(e.Exception, "UnobservedTask");
            e.SetObserved();
        }
    }
}
