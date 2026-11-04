using System.Diagnostics;
using System.IO;
using System.Text;

namespace SonarController.Helpers;

/// <summary>
/// %LocalAppData%\SonarController\logs へのファイルログ。サイズでローテーションし、
/// ユーザー名・ユーザープロファイルのパスはマスクして書き出す。
/// </summary>
public static class AppLog
{
    public const long DefaultMaxFileBytes = 512 * 1024;
    public const int DefaultMaxFiles = 5;

    private const string FileName = "app.log";
    private static readonly object Gate = new();

    private static string? _directory;
    private static long _maxFileBytes = DefaultMaxFileBytes;
    private static int _maxFiles = DefaultMaxFiles;

    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SonarController", "logs");

    public static void Initialize(string? directory = null, long maxFileBytes = DefaultMaxFileBytes, int maxFiles = DefaultMaxFiles)
    {
        lock (Gate)
        {
            _directory = directory ?? DefaultDirectory;
            _maxFileBytes = maxFileBytes;
            _maxFiles = Math.Max(1, maxFiles);
        }
    }

    /// <summary>既存のTrace出力もログファイルへ流す。</summary>
    public static void AttachToTrace()
    {
        if (Trace.Listeners.OfType<AppLogTraceListener>().Any()) return;
        Trace.Listeners.Add(new AppLogTraceListener());
    }

    public static void Info(string message) => Write("INFO", message, null);

    public static void Warn(string message, Exception? exception = null) => Write("WARN", message, exception);

    public static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    public static string Sanitize(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var result = text;
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(profile))
            result = result.Replace(profile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);

        var user = Environment.UserName;
        // 短い名前は一般的な語を壊すためマスク対象外
        if (user.Length >= 3)
            result = result.Replace(user, "<user>", StringComparison.OrdinalIgnoreCase);

        var machine = Environment.MachineName;
        if (machine.Length >= 3)
            result = result.Replace(machine, "<machine>", StringComparison.OrdinalIgnoreCase);

        return result;
    }

    internal static void Write(string level, string message, Exception? exception)
    {
        try
        {
            lock (Gate)
            {
                _directory ??= DefaultDirectory;
                Directory.CreateDirectory(_directory);

                var path = Path.Combine(_directory, FileName);
                RotateIfNeeded(path);

                var builder = new StringBuilder()
                    .Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"))
                    .Append(" [").Append(level).Append("] ")
                    .Append(message);
                if (exception is not null)
                    builder.AppendLine().Append(exception.GetType().Name).Append(": ").Append(exception.Message)
                        .AppendLine().Append(exception.StackTrace);

                File.AppendAllText(path, Sanitize(builder.ToString()) + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // ログ出力の失敗でアプリを止めない
        }
    }

    private static void RotateIfNeeded(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length < _maxFileBytes) return;

        var oldest = $"{path}.{_maxFiles - 1}";
        if (File.Exists(oldest)) File.Delete(oldest);

        for (var i = _maxFiles - 2; i >= 1; i--)
        {
            var from = $"{path}.{i}";
            if (File.Exists(from)) File.Move(from, $"{path}.{i + 1}", overwrite: true);
        }

        if (_maxFiles > 1)
            File.Move(path, $"{path}.1", overwrite: true);
        else
            File.Delete(path);
    }

    private sealed class AppLogTraceListener : TraceListener
    {
        public override void Write(string? message) => AppLog.Write("TRACE", message ?? string.Empty, null);

        public override void WriteLine(string? message) => AppLog.Write("TRACE", message ?? string.Empty, null);

        public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? message)
            => AppLog.Write(eventType.ToString().ToUpperInvariant(), message ?? string.Empty, null);

        public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? format, params object?[]? args)
            => AppLog.Write(eventType.ToString().ToUpperInvariant(),
                args is { Length: > 0 } && format is not null ? string.Format(format, args) : format ?? string.Empty, null);
    }
}
