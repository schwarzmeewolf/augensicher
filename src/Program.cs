using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace Augensicher
{
    static class Program
    {
        // One copy per user session. The old PowerShell version had no guard,
        // and starting it twice meant two icons and two breaks back to back.
        const string MutexName = @"Local\Augensicher-running";
        const string ShowEventName = @"Local\Augensicher-show";

        [STAThread]
        static int Main(string[] args)
        {
            Native.SetProcessDPIAware();   // sharp text at 125% and 150% instead of a blurry stretch
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Development aid: draws the windows into PNG files for the README
            // without putting anything on the screen.
            if (args.Length == 2 && args[0] == "--screenshots")
                return Screenshots.Run(args[1]);

            // For scripted installs: the same as the tray menu item, then exit.
            if (args.Length == 2 && args[0] == "--autostart")
            {
                Log.Init(Settings.DataFolder());
                try
                {
                    if (args[1] == "on") Autostart.Enable(); else Autostart.Disable();
                    return 0;
                }
                catch (Exception e) { Log.Error("--autostart", e); return 1; }
            }

            bool first;
            using (var mutex = new Mutex(true, MutexName, out first))
            using (var show = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName))
            {
                if (!first)
                {
                    show.Set();   // the running copy shows "already running"
                    return 0;
                }

                string folder = Settings.DataFolder();
                Log.Init(folder);
                Application.ThreadException += (s, e) => Log.Error("UI thread", e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (s, e) => Log.Write("FATAL: " + e.ExceptionObject);

                Settings settings;
                try { settings = Settings.Load(Settings.FilePath()); }
                catch (Exception e) { Log.Error("load settings", e); settings = new Settings(); }
                Tr.SetLanguage(settings.Language);

                var app = new TrayApp(folder, settings, show);
                if (Array.IndexOf(args, "--break-now") >= 0) app.StartBreak();
                Application.Run(app);
                GC.KeepAlive(mutex);
            }
            return 0;
        }

        // The eye icon, built into the exe from assets\augensicher.ico.
        public static Icon AppIcon(int size)
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("augensicher.ico"))
                return new Icon(s, size, size);
        }
    }

    static class Screenshots
    {
        public static int Run(string dir)
        {
            Directory.CreateDirectory(dir);
            foreach (string lang in new[] { "en", "ru" })
            {
                Tr.SetLanguage(lang);

                var b = new BreakForm(new Rectangle(-20000, -20000, 1280, 720), true, 5);
                b.ShowState(Tr.Tips[0], 287, "14:05:13");
                Save(b, Path.Combine(dir, "break-" + lang + ".png"));

                var m = new MiniBreakForm();
                m.Location = new Point(-20000, -20000);
                m.ShowState(18);
                Save(m, Path.Combine(dir, "short-break-" + lang + ".png"));

                var s = new SettingsForm(new Settings(), true);
                s.StartPosition = FormStartPosition.Manual;
                s.Location = new Point(-20000, -20000);
                Save(s, Path.Combine(dir, "settings-" + lang + ".png"));

                var a = new AboutForm();
                a.StartPosition = FormStartPosition.Manual;
                a.Location = new Point(-20000, -20000);
                Save(a, Path.Combine(dir, "about-" + lang + ".png"));
            }
            return 0;
        }

        static void Save(Form f, string path)
        {
            f.Show();
            Application.DoEvents();
            using (var bmp = new Bitmap(f.Width, f.Height))
            {
                f.DrawToBitmap(bmp, new Rectangle(Point.Empty, f.Size));
                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
            f.Close();
        }
    }
}
