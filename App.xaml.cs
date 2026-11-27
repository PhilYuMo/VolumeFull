using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace VolumeCtrlWpf
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 崩溃日志：写入 %LOCALAPPDATA%\VolumeFull\crash.log，用户反馈问题时可直接附上。
            // 只记录，不改变默认的崩溃行为（UI 线程异常仍会终止进程）。
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
                WriteCrashLog(args.ExceptionObject as Exception, "后台线程");
            DispatcherUnhandledException += (s, args) =>
                WriteCrashLog(args.Exception, "UI 线程");
            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                WriteCrashLog(args.Exception, "未观察的任务");
                args.SetObserved();
            };
        }

        private static void WriteCrashLog(Exception ex, string source)
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "VolumeFull");
                Directory.CreateDirectory(dir);

                var sb = new StringBuilder();
                sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}");
                sb.AppendLine($"Version: {typeof(App).Assembly.GetName().Version}");
                sb.AppendLine(ex?.ToString() ?? "(no exception object)");
                sb.AppendLine(new string('-', 80));

                File.AppendAllText(Path.Combine(dir, "crash.log"), sb.ToString(), Encoding.UTF8);
            }
            catch
            {
                // 日志写入失败时不能再抛异常
            }
        }
    }
}
