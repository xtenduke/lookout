namespace Lookout.Runner.Util;

using System.Runtime.CompilerServices;

public static class Logger
{
    private enum LogLevel { Debug, Info, Error}

    public static void Info(string message,
        [CallerMemberName] string caller = "",
        [CallerFilePath] string filePath = "",
        params object[] args)
    {
        Log(LogLevel.Info, message, caller, filePath, args);
    }

    public static void Debug(string message,
        [CallerMemberName] string caller = "",
        [CallerFilePath] string filePath = "",
        params object[] args)
    {
        // Check the loglevel here... probably an application arg setting a global
        Log(LogLevel.Debug, message, caller, filePath, args);
    }

    public static void Error(string message,
        [CallerMemberName] string caller = "",
        [CallerFilePath] string filePath = "",
        params object[] args)
    {
        Log(LogLevel.Error, message, caller, filePath, args);
    }

    private static void Log(LogLevel level, string message, string caller, string filePath, object[] args)
    {
        var className = Path.GetFileNameWithoutExtension(filePath);

        string formattedMessage = args.Length > 0 ? string.Format(message, args) : message;

        var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level.ToString()}] [{className}.{caller}] {formattedMessage}";

        lock (Console.Out)
        {
            Console.ForegroundColor = level switch
            {
                LogLevel.Error => ConsoleColor.Red,
                LogLevel.Info => ConsoleColor.Green,
                LogLevel.Debug => ConsoleColor.Green,
                _ => Console.ForegroundColor
            };

            Console.WriteLine(logMessage);
            Console.ResetColor();
        }
    }
}