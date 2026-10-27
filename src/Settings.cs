using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Augensicher
{
    // Settings as a plain key=value file, so a person can read and fix it in
    // Notepad. Unknown keys are ignored and bad values fall back to defaults:
    // a damaged file never stops the program from starting.
    public sealed class Settings
    {
        public int WorkMinutes = 45;
        public int BreakMinutes = 5;
        public int SnoozeMinutes = 5;
        public int WarnSeconds = 30;
        public bool AllowSkip = true;
        public bool Sound = true;
        public bool MiniBreaks = false;
        public int MiniEveryMinutes = 20;
        public int MiniSeconds = 20;
        public int IdlePauseMinutes = 2;
        public int IdleResetMinutes = 5;
        public bool FullScreenIsWork = true;
        public string Language = "auto";   // auto, en, ru

        public const string FileName = "settings.ini";

        // settings.ini next to the exe makes a portable copy: settings and log
        // stay in that folder. Otherwise they live in %APPDATA%\Augensicher.
        public static string DataFolder()
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            if (File.Exists(Path.Combine(exeDir, FileName))) return exeDir;
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppInfo.Name);
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static string FilePath() { return Path.Combine(DataFolder(), FileName); }

        public static Settings Load(string path)
        {
            if (!File.Exists(path)) return new Settings();
            return Parse(File.ReadAllText(path, Encoding.UTF8));
        }

        public void Save(string path)
        {
            File.WriteAllText(path, Serialize(), new UTF8Encoding(false));
        }

        public static Settings Parse(string text)
        {
            var s = new Settings();
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                map[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }

            s.WorkMinutes = Int(map, "work_minutes", s.WorkMinutes);
            s.BreakMinutes = Int(map, "break_minutes", s.BreakMinutes);
            s.SnoozeMinutes = Int(map, "snooze_minutes", s.SnoozeMinutes);
            s.WarnSeconds = Int(map, "warn_seconds", s.WarnSeconds);
            s.AllowSkip = Bool(map, "allow_skip", s.AllowSkip);
            s.Sound = Bool(map, "sound", s.Sound);
            s.MiniBreaks = Bool(map, "mini_breaks", s.MiniBreaks);
            s.MiniEveryMinutes = Int(map, "mini_every_minutes", s.MiniEveryMinutes);
            s.MiniSeconds = Int(map, "mini_seconds", s.MiniSeconds);
            s.IdlePauseMinutes = Int(map, "idle_pause_minutes", s.IdlePauseMinutes);
            s.IdleResetMinutes = Int(map, "idle_reset_minutes", s.IdleResetMinutes);
            s.FullScreenIsWork = Bool(map, "fullscreen_is_work", s.FullScreenIsWork);
            string lang;
            if (map.TryGetValue("language", out lang)) s.Language = lang.ToLowerInvariant();
            s.Normalize();
            return s;
        }

        public string Serialize()
        {
            var b = new StringBuilder();
            b.AppendLine("# " + AppInfo.Name + " settings. The Settings window writes this file;");
            b.AppendLine("# you can also edit it by hand and restart the program.");
            b.AppendLine("work_minutes = " + WorkMinutes);
            b.AppendLine("break_minutes = " + BreakMinutes);
            b.AppendLine("snooze_minutes = " + SnoozeMinutes);
            b.AppendLine("warn_seconds = " + WarnSeconds);
            b.AppendLine("allow_skip = " + Flag(AllowSkip));
            b.AppendLine("sound = " + Flag(Sound));
            b.AppendLine("mini_breaks = " + Flag(MiniBreaks));
            b.AppendLine("mini_every_minutes = " + MiniEveryMinutes);
            b.AppendLine("mini_seconds = " + MiniSeconds);
            b.AppendLine("idle_pause_minutes = " + IdlePauseMinutes);
            b.AppendLine("idle_reset_minutes = " + IdleResetMinutes);
            b.AppendLine("fullscreen_is_work = " + Flag(FullScreenIsWork));
            b.AppendLine("language = " + Language);
            return b.ToString();
        }

        // Keeps every value inside the range the Settings window allows.
        public void Normalize()
        {
            WorkMinutes = Clamp(WorkMinutes, 1, 240);
            BreakMinutes = Clamp(BreakMinutes, 1, 60);
            SnoozeMinutes = Clamp(SnoozeMinutes, 1, 60);
            WarnSeconds = Clamp(WarnSeconds, 0, 300);
            MiniEveryMinutes = Clamp(MiniEveryMinutes, 5, 120);
            MiniSeconds = Clamp(MiniSeconds, 5, 120);
            IdlePauseMinutes = Clamp(IdlePauseMinutes, 1, 30);
            IdleResetMinutes = Clamp(IdleResetMinutes, IdlePauseMinutes, 120);
            if (Language != "en" && Language != "ru") Language = "auto";
        }

        public void ApplyTo(WorkTracker t)
        {
            t.WorkSeconds = WorkMinutes * 60;
            t.IdlePauseSeconds = IdlePauseMinutes * 60;
            t.IdleResetSeconds = IdleResetMinutes * 60;
            t.WarnSeconds = WarnSeconds;
            t.MiniEverySeconds = MiniBreaks ? MiniEveryMinutes * 60 : 0;
            t.FullScreenIsWork = FullScreenIsWork;
        }

        static int Clamp(int v, int min, int max) { return v < min ? min : (v > max ? max : v); }

        static string Flag(bool v) { return v ? "true" : "false"; }

        static int Int(Dictionary<string, string> map, string key, int fallback)
        {
            string v; int n;
            if (map.TryGetValue(key, out v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) return n;
            return fallback;
        }

        static bool Bool(Dictionary<string, string> map, string key, bool fallback)
        {
            string v;
            if (!map.TryGetValue(key, out v)) return fallback;
            v = v.ToLowerInvariant();
            if (v == "true" || v == "yes" || v == "1" || v == "on") return true;
            if (v == "false" || v == "no" || v == "0" || v == "off") return false;
            return fallback;
        }
    }
}
