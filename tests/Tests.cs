using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Augensicher.Tests
{
    // A tiny test runner: no NuGet, no test framework, so the tests build with
    // the same compiler as the program. Exit code 0 = all passed.
    static class TestMain
    {
        static int failed, passed;

        static int Main()
        {
            Run("work counts up to a break", WorkCountsToBreak);
            Run("warning comes once, before the break", WarningOnce);
            Run("a short idle stops the count and gives back the threshold", IdleGivesBackThreshold);
            Run("a long idle starts over", LongIdleStartsOver);
            Run("a locked screen is away at once", LockedIsAway);
            Run("full screen counts as work without input", FullScreenIsWork);
            Run("full screen does not count when switched off", FullScreenOff);
            Run("sleep longer than the reset starts over", SleepStartsOver);
            Run("a short sleep keeps the count", ShortSleepKeeps);
            Run("snooze leaves exactly the snooze time", SnoozeLeaves);
            Run("short breaks come every N minutes", MiniBreaks);
            Run("no short break right before the long one", NoMiniBeforeLong);
            Run("settings survive a round trip", SettingsRoundTrip);
            Run("bad settings fall back to safe values", SettingsBadValues);
            Run("every string in the code has a Russian translation", TranslationsComplete);

            Console.WriteLine();
            Console.WriteLine("{0} passed, {1} failed", passed, failed);
            return failed == 0 ? 0 : 1;
        }

        static void Run(string name, Action test)
        {
            try { test(); passed++; Console.WriteLine("  ok   " + name); }
            catch (Exception e) { failed++; Console.WriteLine("  FAIL " + name + ": " + e.Message); }
        }

        static void Check(bool condition, string what)
        {
            if (!condition) throw new Exception(what);
        }

        static void Equal(object expected, object actual, string what)
        {
            if (!Equals(expected, actual)) throw new Exception(what + ": expected " + expected + ", got " + actual);
        }

        // ---------------------------------------------------------------- helpers

        static WorkTracker Tracker()
        {
            return new WorkTracker { WorkSeconds = 600, IdlePauseSeconds = 120, IdleResetSeconds = 300, WarnSeconds = 30, MiniEverySeconds = 0 };
        }

        static TickInput Working() { return new TickInput { GapSeconds = 1, RawIdleSeconds = 0 }; }

        static TickInput Idle(int raw) { return new TickInput { GapSeconds = 1, RawIdleSeconds = raw }; }

        // Ticks as a person who works and touches the mouse now and then.
        static List<TickResult> Work(WorkTracker t, int seconds)
        {
            var events = new List<TickResult>();
            for (int i = 0; i < seconds; i++)
            {
                var r = t.Tick(Working());
                if (r != TickResult.None) events.Add(r);
            }
            return events;
        }

        // ---------------------------------------------------------------- tracker

        static void WorkCountsToBreak()
        {
            var t = Tracker();
            var events = Work(t, 599);
            Check(!events.Contains(TickResult.Break), "no break before 600 s");
            Equal(TickResult.Break, t.Tick(Working()), "break at 600 s");
        }

        static void WarningOnce()
        {
            var t = Tracker();
            var events = Work(t, 599);
            Equal(1, events.FindAll(e => e == TickResult.Warn).Count, "one warning");        }

        static void IdleGivesBackThreshold()
        {
            var t = Tracker();
            Work(t, 300);
            // 119 s without input still looks like work...
            for (int i = 1; i < 120; i++) t.Tick(Idle(i));
            Equal(419, t.Worked, "counted while under the threshold");
            Check(!t.IsIdle, "not away yet");
            // ...at 120 s the person is away, and those 120 s are given back.
            t.Tick(Idle(120));
            Check(t.IsIdle, "away at the threshold");
            Equal(299, t.Worked, "threshold given back");
            t.Tick(Idle(121));
            Equal(299, t.Worked, "count stays while away");
            Equal(TickResult.None, t.Tick(Working()), "back to work");
            Equal(300, t.Worked, "counting again");
        }

        static void LongIdleStartsOver()
        {
            var t = Tracker();
            Work(t, 400);
            for (int i = 1; i <= 300; i++) t.Tick(Idle(i));
            Equal(0, t.Worked, "start over after 300 s away");
        }

        static void LockedIsAway()
        {
            var t = Tracker();
            Work(t, 100);
            t.Tick(new TickInput { GapSeconds = 1, RawIdleSeconds = 3, Locked = true });
            Check(t.IsIdle, "away");
            Equal(AwayReason.Locked, t.Reason, "reason");
            Equal(97, t.Worked, "the 3 s since the last input are given back");
        }

        static void FullScreenIsWork()
        {
            var t = Tracker();
            for (int i = 1; i <= 200; i++) t.Tick(new TickInput { GapSeconds = 1, RawIdleSeconds = i, FullScreen = true });
            Check(!t.IsIdle, "watching a film is not away");
            Equal(200, t.Worked, "all counted");
        }

        static void FullScreenOff()
        {
            var t = Tracker();
            t.FullScreenIsWork = false;
            for (int i = 1; i <= 200; i++) t.Tick(new TickInput { GapSeconds = 1, RawIdleSeconds = i, FullScreen = true });
            Check(t.IsIdle, "away when full screen does not count");
        }

        static void SleepStartsOver()
        {
            var t = Tracker();
            Work(t, 400);
            t.Tick(new TickInput { GapSeconds = 3600 });
            Equal(0, t.Worked, "an hour asleep starts over");
            Equal(AwayReason.Asleep, t.Reason, "reason");
        }

        static void ShortSleepKeeps()
        {
            var t = Tracker();
            Work(t, 400);
            t.Tick(new TickInput { GapSeconds = 60 });
            Equal(400, t.Worked, "a minute asleep keeps the count");
        }

        static void SnoozeLeaves()
        {
            var t = Tracker();
            Work(t, 600);
            t.Snooze(120);
            Equal(120, t.SecondsToBreak, "two minutes left");
            var events = Work(t, 120);
            Check(events.Contains(TickResult.Warn), "warned again after snooze");
            Equal(TickResult.Break, events[events.Count - 1], "break after the snooze");
        }

        static void MiniBreaks()
        {
            var t = Tracker();
            t.WorkSeconds = 3600;
            t.MiniEverySeconds = 600;
            var events = Work(t, 1800);
            Equal(3, events.FindAll(e => e == TickResult.MiniBreak).Count, "three short breaks in 30 min");
        }

        static void NoMiniBeforeLong()
        {
            var t = Tracker();
            t.WorkSeconds = 1200;
            t.MiniEverySeconds = 600;
            t.WarnSeconds = 0;
            var events = Work(t, 1200);
            // At 600 s one short break; at 1200 s the long one, not both.
            Equal(1, events.FindAll(e => e == TickResult.MiniBreak).Count, "one short break");
            Equal(TickResult.Break, events[events.Count - 1], "then the long break");
        }

        // ---------------------------------------------------------------- settings

        static void SettingsRoundTrip()
        {
            var s = new Settings { WorkMinutes = 50, BreakMinutes = 3, MiniBreaks = true, AllowSkip = false, Language = "ru" };
            var r = Settings.Parse(s.Serialize());
            Equal(50, r.WorkMinutes, "work");
            Equal(3, r.BreakMinutes, "break");
            Equal(true, r.MiniBreaks, "mini");
            Equal(false, r.AllowSkip, "skip");
            Equal("ru", r.Language, "language");
        }

        static void SettingsBadValues()
        {
            var r = Settings.Parse("work_minutes = lots\nbreak_minutes = -4\nidle_pause_minutes = 10\nidle_reset_minutes = 2\nlanguage = klingon\nsound = maybe\ngarbage line\n");
            Equal(45, r.WorkMinutes, "unreadable number -> default");
            Equal(1, r.BreakMinutes, "negative -> minimum");
            Equal(10, r.IdleResetMinutes, "reset is never shorter than pause");
            Equal("auto", r.Language, "unknown language -> auto");
            Equal(true, r.Sound, "unreadable flag -> default");
        }

        // ---------------------------------------------------------------- translations

        // Every literal passed to Tr.T or Tr.F in src\*.cs must be in the table.
        static void TranslationsComplete()
        {
            // tests.exe runs from build\, next to src\
            string src = Path.Combine(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\')), "src");
            var missing = new List<string>();
            int seen = 0;
            foreach (string file in Directory.GetFiles(src, "*.cs"))
            {
                foreach (Match m in Regex.Matches(File.ReadAllText(file), @"Tr\.[TF]\(""((?:[^""\\]|\\.)*)"""))
                {
                    seen++;
                    string key = Regex.Unescape(m.Groups[1].Value);
                    if (!Tr.Russian.ContainsKey(key)) missing.Add(key);
                }
            }
            Check(seen > 40, "found the strings (" + seen + ")");
            Check(missing.Count == 0, "no Russian for: " + string.Join(" | ", missing));
        }
    }
}
