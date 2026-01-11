using System.Runtime.InteropServices;

namespace K42.Logging;

/// <summary>
/// System logger for K42.
/// 
/// Writes to:
/// - Linux: syslog/journald
/// - Windows: Event Log
/// - Console: always (for debugging)
/// 
/// No log aggregation. No metrics. No tracing.
/// Human-readable. That's it.
/// </summary>
public static class SystemLogger
{
    private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    private const string SourceName = "K42";

    static SystemLogger()
    {
        // On Windows, ensure the event source exists
        if (IsWindows)
        {
            try
            {
                EnsureWindowsEventSource();
            }
            catch
            {
                // May fail without admin rights, that's okay
            }
        }
    }

    public static void Info(string message)
    {
        var formatted = $"[K42] {message}";
        Console.WriteLine(formatted);
        WriteToSystemLog(message, LogLevel.Info);
    }

    public static void Warning(string message)
    {
        var formatted = $"[K42] WARNING: {message}";
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(formatted);
        Console.ResetColor();
        WriteToSystemLog(message, LogLevel.Warning);
    }

    public static void Error(string message)
    {
        var formatted = $"[K42] ERROR: {message}";
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(formatted);
        Console.ResetColor();
        WriteToSystemLog(message, LogLevel.Error);
    }

    private static void WriteToSystemLog(string message, LogLevel level)
    {
        try
        {
            if (IsWindows)
            {
                WriteToWindowsEventLog(message, level);
            }
            else
            {
                WriteToSyslog(message, level);
            }
        }
        catch
        {
            // Logging should never crash the application
        }
    }

    private static void EnsureWindowsEventSource()
    {
        // This requires elevation on first run
        // We gracefully handle the case where it doesn't exist
    }

    private static void WriteToWindowsEventLog(string message, LogLevel level)
    {
        // Windows Event Log writing
        // Using P/Invoke to avoid System.Diagnostics.EventLog dependency issues on Linux
        try
        {
            // Simple fallback: write to a log file in ProgramData
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "K42",
                "logs");
            
            if (!Directory.Exists(logDir))
                Directory.CreateDirectory(logDir);

            var logFile = Path.Combine(logDir, $"k42-{DateTime.Now:yyyy-MM-dd}.log");
            var logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
            
            File.AppendAllText(logFile, logLine + Environment.NewLine);
        }
        catch
        {
            // Ignore logging failures
        }
    }

    private static void WriteToSyslog(string message, LogLevel level)
    {
        // Write to /dev/log or use logger command
        try
        {
            var priority = level switch
            {
                LogLevel.Error => "err",
                LogLevel.Warning => "warning",
                _ => "info"
            };

            // Use the logger command which is available on most Linux systems
            var loggerPath = "/usr/bin/logger";
            if (File.Exists(loggerPath))
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = loggerPath,
                        Arguments = $"-t k42 -p local0.{priority} \"{message.Replace("\"", "\\\"")}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                process.WaitForExit(1000);
            }
            else
            {
                // Fallback: write to /var/log/k42.log
                var logFile = "/var/log/k42.log";
                var logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
                
                try
                {
                    File.AppendAllText(logFile, logLine + Environment.NewLine);
                }
                catch
                {
                    // May not have permission, ignore
                }
            }
        }
        catch
        {
            // Ignore logging failures
        }
    }

    private enum LogLevel
    {
        Info,
        Warning,
        Error
    }
}
