using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.ReactiveUI;
using SupportAssistant.Core.Engines;

namespace SupportAssistant;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // WS5: this is the insertion point for a `--worker inference` branch calling
        // InferenceWorkerHost.RunAsync(...) once out-of-process engine wiring lands (real fix for the
        // Mesa/MIGraphX LLVM collision — see docs/plans/inference-engine-integration-status.md §C and
        // inference-integration-implementation-plan.md §6). Today args always fall through to the GUI.

        // Surface otherwise-silent crashes (unhandled exceptions on any thread,
        // including unobserved task exceptions) to a log file and stderr.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            WriteCrashLog("AppDomain.UnhandledException", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            // Mark observed so .NET does not treat this as a fatal unobserved-task failure
            // (which can terminate the process / yield a non-zero exit after GC finalization).
            e.SetObserved();

            if (IsBenignLinuxImeDisposeException(e.Exception))
            {
                LogSwallowedNonFatal(
                    "SetObserved: Linux IME/DBus dispose (known Avalonia+ibus-portal upstream issue)",
                    e.Exception);
                return;
            }

            LogSwallowedNonFatal(
                "SetObserved: unobserved task fault marked observed so exit stays clean",
                e.Exception);
            WriteCrashLog("TaskScheduler.UnobservedTaskException", e.Exception);
        };

        // Loud early warning for gfx1031/etc. hosts missing HSA_OVERRIDE_GFX_VERSION.
        // InferenceOptionsFactory also forces CPU in that case; this surfaces the reason before UI.
        LogRocmPreflightIfNeeded();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Avalonia's IBus DBus IME hits an ibus-portal bug on Destroy (UnknownMethod on
    /// org.freedesktop.IBus.Service). Default off; opt in with AVALONIA_IM_MODULE=ibus|fcitx|xim
    /// when CJK composition is required (may log benign dispose noise until upstream is fixed).
    /// </summary>
    private static bool? ResolveLinuxImePreference()
    {
        var module = Environment.GetEnvironmentVariable("AVALONIA_IM_MODULE");
        if (string.Equals(module, "none", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(module, "ibus", StringComparison.OrdinalIgnoreCase)
            || string.Equals(module, "fcitx", StringComparison.OrdinalIgnoreCase)
            || string.Equals(module, "xim", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool IsBenignLinuxImeDisposeException(Exception? ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    if (IsBenignLinuxImeDisposeException(inner))
                    {
                        return true;
                    }
                }

                continue;
            }

            var message = current.Message ?? string.Empty;
            if (message.Contains("org.freedesktop.IBus", StringComparison.Ordinal)
                || message.Contains("InputContext_", StringComparison.Ordinal))
            {
                return true;
            }

            var typeName = current.GetType().FullName ?? string.Empty;
            if (typeName.Contains("DBusException", StringComparison.Ordinal)
                && message.Contains("Destroy", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void LogSwallowedNonFatal(string reason, Exception? ex)
    {
        try
        {
            var detail = SummarizeExceptionOneLine(ex);
            Console.Error.WriteLine(
                $"[non-fatal] Swallowed exception ({reason}): {detail}");
        }
        catch
        {
            // Logging must never throw.
        }
    }

    private static string SummarizeExceptionOneLine(Exception? ex)
    {
        if (ex is null)
        {
            return "(null)";
        }

        // Prefer the innermost useful exception; AggregateException → first inner.
        var leaf = ex;
        while (leaf is AggregateException { InnerExceptions.Count: > 0 } agg)
        {
            leaf = agg.InnerExceptions[0];
        }

        while (leaf.InnerException is not null)
        {
            leaf = leaf.InnerException;
        }

        var type = leaf.GetType().Name;
        var message = (leaf.Message ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ').Trim();
        if (message.Length > 160)
        {
            message = message[..160] + "…";
        }

        return string.IsNullOrEmpty(message) ? type : $"{type}: {message}";
    }

    private static void LogRocmPreflightIfNeeded()
    {
        try
        {
            var result = RocmHostPreflight.Evaluate();
            if (result.Status != RocmPreflightStatus.OverrideRequired)
            {
                return;
            }

            Console.Error.WriteLine(result.Message);
            var path = Path.Combine(AppContext.BaseDirectory, "supportassistant-rocm-preflight.log");
            File.WriteAllText(path, $"[{DateTime.Now:O}]\n{result.Message}\n");
        }
        catch
        {
            // Preflight must never block startup.
        }
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
            .With(new X11PlatformOptions
            {
                EnableIme = ResolveLinuxImePreference(),
            })
            .WithInterFont()
            .UseReactiveUI()
            .LogToTrace();
}
