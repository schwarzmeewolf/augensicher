using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Augensicher
{
    // One file with what a helper needs to know. Someone who hit a problem
    // cannot be asked to find a log in %APPDATA%; the tray offers "Support
    // info" instead, which writes this file and shows it in Explorer.
    //
    // What goes in: versions, screens, settings, the end of the log. The log
    // holds only the program's own events, so there is nothing private to cut.
    static class SupportInfo
    {
        public static string Write(string folder, bool autostart)
        {
            var b = new StringBuilder();
            b.AppendLine(AppInfo.Name + " support info, " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            b.AppendLine();
            b.AppendLine("Version:   " + AppInfo.Version);
            b.AppendLine("Program:   " + Application.ExecutablePath);
            b.AppendLine("Data:      " + folder);
            b.AppendLine("Windows:   " + WindowsVersion());
            b.AppendLine(".NET:      " + Environment.Version + " (release " + DotNetRelease() + ")");
            b.AppendLine("UI lang:   " + System.Globalization.CultureInfo.CurrentUICulture.Name + " -> " + Tr.Lang);
            b.AppendLine("Autostart: " + (autostart ? "on" : "off"));
            using (var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero)) b.AppendLine("DPI:       " + g.DpiX);
            foreach (var s in Screen.AllScreens)
                b.AppendLine("Screen:    " + s.Bounds.Width + "x" + s.Bounds.Height + " at " + s.Bounds.X + "," + s.Bounds.Y + (s.Primary ? " (primary)" : ""));
            b.AppendLine();

            b.AppendLine("--- settings ---");
            string settings = Path.Combine(folder, Settings.FileName);
            b.AppendLine(File.Exists(settings) ? File.ReadAllText(settings, Encoding.UTF8) : "(no settings file: defaults)");

            b.AppendLine("--- last lines of the log ---");
            if (Log.FilePath != null && File.Exists(Log.FilePath))
            {
                string[] lines = ReadShared(Log.FilePath);
                foreach (string l in lines.Skip(Math.Max(0, lines.Length - 80))) b.AppendLine(l);
            }
            else b.AppendLine("(no log)");

            string path = Path.Combine(folder, "support-info.txt");
            File.WriteAllText(path, b.ToString(), new UTF8Encoding(false));
            Log.Write("support file written");
            return path;
        }

        public static void ShowInExplorer(string path)
        {
            Process.Start("explorer.exe", "/select,\"" + path + "\"");
        }

        static string[] ReadShared(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var r = new StreamReader(fs, Encoding.UTF8))
                return r.ReadToEnd().Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        }

        public static string WindowsVersion()
        {
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    if (k == null) return Environment.OSVersion.ToString();
                    string build = k.GetValue("CurrentBuild") as string ?? "";
                    // ProductName still says "Windows 10" on Windows 11; the build number tells them apart.
                    string name = k.GetValue("ProductName") as string ?? "Windows";
                    int n;
                    if (int.TryParse(build, out n) && n >= 22000) name = name.Replace("Windows 10", "Windows 11");
                    return name + " " + (k.GetValue("DisplayVersion") as string ?? "") + " build " + build + "." + k.GetValue("UBR");
                }
            }
            catch { return Environment.OSVersion.ToString(); }
        }

        static string DotNetRelease()
        {
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))
                    return k == null ? "?" : Convert.ToString(k.GetValue("Release"));
            }
            catch { return "?"; }
        }
    }
}
