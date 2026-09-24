#if DIAG_LOG
using System.IO;

namespace BatteryBuddy.App;

/// <summary>
/// Diagnostics for the default (framework-dependent) build only; build.ps1 leaves it out of -SelfContained.
/// Keeps just the last <see cref="Window"/> of lines, rewriting logs\diag.log on every <see cref="Flush"/>.
/// </summary>
static class DiagLog
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(5);
    static readonly object Gate = new();
    static readonly Queue<(DateTime At, string Line)> Lines = new();
    static readonly string FilePath = Path.Combine(Log.Dir, "diag.log");
    static bool _keptPrevious;

    public static void Write(string message)
    {
        var now = DateTime.Now;
        lock (Gate)
        {
            Lines.Enqueue((now, $"{now:HH:mm:ss.fff} {message}"));
            while (Lines.Peek().At < now - Window) Lines.Dequeue();
        }
    }

    public static void Flush()
    {
        string[] snapshot;
        lock (Gate) snapshot = Lines.Select(l => l.Line).ToArray();
        try
        {
            Directory.CreateDirectory(Log.Dir);
            if (!_keptPrevious)
            {
                // Restarting a blank pet shouldn't wipe what the last run saw.
                if (File.Exists(FilePath)) File.Move(FilePath, Path.Combine(Log.Dir, "diag.previous.log"), overwrite: true);
                _keptPrevious = true;
            }
            var temp = FilePath + ".tmp";
            File.WriteAllLines(temp, snapshot);
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
#endif
