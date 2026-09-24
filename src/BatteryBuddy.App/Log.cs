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

    public static void Prune(int days = 7)
    {
        if (!Directory.Exists(Dir)) return;
        foreach (var file in Directory.GetFiles(Dir, "battery-buddy-*.log"))
            if (File.GetLastWriteTime(file) < DateTime.Now.AddDays(-days))
                File.Delete(file);
    }
}
