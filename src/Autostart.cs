using System;
using System.IO;
using System.Windows.Forms;

namespace Augensicher
{
    // "Start with Windows" is a shortcut in the user's Startup folder: no
    // registry, no administrator rights, and visible in Task Manager's
    // Startup tab where the person can turn it off too.
    static class Autostart
    {
        static string LinkPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), AppInfo.Name + ".lnk"); }
        }

        // On only when the shortcut starts this very copy: a shortcut left by
        // a copy in another folder does not count.
        public static bool IsEnabled()
        {
            try
            {
                if (!File.Exists(LinkPath)) return false;
                return string.Equals(Path.GetFullPath(ShellLink.GetTarget(LinkPath)), Path.GetFullPath(Application.ExecutablePath), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception e)
            {
                Log.Error("Autostart.IsEnabled", e);
                return false;
            }
        }

        public static void Enable()
        {
            ShellLink.Create(LinkPath, Application.ExecutablePath, "Eye break reminder");
            Log.Write("autostart on: " + LinkPath);
        }

        public static void Disable()
        {
            if (File.Exists(LinkPath)) File.Delete(LinkPath);
            Log.Write("autostart off");
        }
    }
}
