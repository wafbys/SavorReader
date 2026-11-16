using System.Text;

namespace SavorReader.Services;

public static class AppLog
{
    private static readonly object Gate = new();

    public static string LogDirectory
    {
        get
        {
            var overridden = Environment.GetEnvironmentVariable("SAVORREADER_DATA_DIR");
            var dataRoot = string.IsNullOrWhiteSpace(overridden)
                ? Path.Combine(AppContext.BaseDirectory, "Data")
                : overridden;
            return Path.Combine(dataRoot, "logs");
        }
    }

    public static void Info(string message) => Write("INFO", message, null);

    public static void Warn(string message, Exception? exception = null) => Write("WARN", message, exception);

    public static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(LogDirectory);
                var builder = new StringBuilder()
                    .Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"))
                    .Append(" [")
                    .Append(level)
                    .Append("] ")
                    .AppendLine(message);
                if (exception is not null)
                {
                    builder.AppendLine(exception.ToString());
                }

                File.AppendAllText(
                    Path.Combine(LogDirectory, "app.log"),
                    builder.ToString(),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
        }
        catch
        {
            // Logging must never take the app down.
        }
    }
}
