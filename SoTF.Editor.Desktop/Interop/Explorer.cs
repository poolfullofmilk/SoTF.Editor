using System.ComponentModel;
using System.Diagnostics;
using NLog;

namespace SoTF.Editor.Desktop.Interop;

public static class Explorer
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    public static void ShowFile(string path)
    {
        Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\""));
    }

    public static void OpenFolder(string folder)
    {
        Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\""));
    }

    public static void OpenLink(string url)
    {
        Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private static void Start(ProcessStartInfo startInfo)
    {
        try
        {
            Process.Start(startInfo)?.Dispose();
        }
        catch (Win32Exception exception)
        {
            // A Failed Shell Launch Never Fails What Asked
            s_logger.Warn(exception, "Failed To Open Shell");
        }
    }
}
