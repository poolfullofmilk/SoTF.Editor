using System.ComponentModel;
using System.Diagnostics;
using NLog;

namespace SoTF.Editor.Desktop.Interop;

public static class Explorer
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    public static void ShowFile(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\""));
        }
        catch (Win32Exception exception)
        {
            // A Shell That Fails To Open Never Fails The Export
            s_logger.Warn(exception, "Failed To Show Export");
        }
    }
}
