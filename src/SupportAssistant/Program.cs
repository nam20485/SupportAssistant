using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.ReactiveUI;

namespace SupportAssistant;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Surface otherwise-silent crashes (unhandled exceptions on any thread,
        // including unobserved task exceptions) to a log file and stderr.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            WriteCrashLog("AppDomain.UnhandledException", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            WriteCrashLog("TaskScheduler.UnobservedTaskException", e.Exception);
            e.SetObserved();
        };

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static void WriteCrashLog(string source, Exception? ex)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "supportassistant-crash.log");
            var text = $"[{DateTime.Now:O}] {source}\n{ex}\n\n";
            File.AppendAllText(path, text);
            Console.Error.WriteLine(text);
        }
        catch
        {
            // Swallow diagnostics failures so they never mask the original problem.
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .UseReactiveUI()
            .LogToTrace();
}
