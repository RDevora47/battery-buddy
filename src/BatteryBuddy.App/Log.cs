using System.IO;

namespace BatteryBuddy.App;

static class Log
{
    static readonly object Gate = new();
    internal static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BatteryBuddy", "logs");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Dir);
                File.AppendAllText(
                    Path.Combine(Dir, $"battery-buddy-{DateTime.Now:yyyyMMdd}.log"),
                    $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>Deletes logs older than <paramref name="days"/>; one it can't delete (open elsewhere, no access) waits for the next start.</summary>
    public static void Prune(int days = 7)
    {
        try
        {
            if (!Directory.Exists(Dir)) return;
            foreach (var file in Directory.GetFiles(Dir, "battery-buddy-*.log"))
                if (File.GetLastWriteTime(file) < DateTime.Now.AddDays(-days))
                    try { File.Delete(file); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
