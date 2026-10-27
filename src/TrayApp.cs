using System;
using System.Collections.Generic;
using System.Drawing;
using System.Media;
using System.Threading;
using System.Windows.Forms;

namespace Augensicher
{
    // The running program: the eye icon, its menu, and the two timers.
    sealed class TrayApp : ApplicationContext
    {
        readonly string dataFolder;
        Settings settings;
        readonly WorkTracker tracker = new WorkTracker();
        readonly PowerWatcher power = new PowerWatcher();
        readonly NotifyIcon notify = new NotifyIcon();
        readonly Icon icon;
        readonly SynchronizationContext ui;
        readonly Random random = new Random();

        // Main tick, once a second: is the person working or away?
        readonly System.Windows.Forms.Timer tickTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        // Break countdowns. Counted against the clock, so a break the computer
        // slept through is simply over when it wakes.
        readonly System.Windows.Forms.Timer breakTimer = new System.Windows.Forms.Timer { Interval = 250 };

        DateTime lastTick = DateTime.Now;
        DateTime? pausedUntil;

        bool inBreak;
        DateTime breakEnd;
        string breakTip;
        readonly List<BreakForm> breakForms = new List<BreakForm>();

        MiniBreakForm miniForm;
        DateTime miniEnd;

        SettingsForm settingsForm;
        AboutForm aboutForm;
        ToolStripMenuItem autostartItem, resumeItem;

        public TrayApp(string dataFolder, Settings settings, EventWaitHandle showSignal)
        {
            this.dataFolder = dataFolder;
            this.settings = settings;
            ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
            settings.ApplyTo(tracker);

            icon = Program.AppIcon(SystemInformation.SmallIconSize.Width);
            notify.Icon = icon;
            notify.Visible = true;
            notify.MouseDoubleClick += delegate { ShowStatusBalloon(); };
            BuildMenu();
            UpdateTray();

            tickTimer.Tick += delegate { Guard("tick", OnTick); };
            breakTimer.Tick += delegate { Guard("break timer", OnBreakTimer); };
            tickTimer.Start();

            // A second copy started by mistake signals this one and leaves.
            if (showSignal != null)
                ThreadPool.RegisterWaitForSingleObject(showSignal, delegate
                {
                    ui.Post(delegate { Balloon(Tr.T("Already running: the eye icon is next to the clock."), 4000); }, null);
                }, null, Timeout.Infinite, false);

            Balloon(Tr.F("Running: a break every {0} min of work, time away does not count. Right-click the eye for settings.", settings.WorkMinutes), 5000);
            Log.Write("started " + AppInfo.Version + " on " + SupportInfo.WindowsVersion() + ", work " + settings.WorkMinutes + " min, break " + settings.BreakMinutes + " min");
        }

        // A failing tick must not end the program: it is written down and the
        // next tick tries again.
        static void Guard(string where, Action action)
        {
            try { action(); }
            catch (Exception e) { Log.Error(where, e); }
        }

        // ------------------------------------------------------------ menu

        void BuildMenu()
        {
            var old = notify.ContextMenuStrip;
            var menu = new ContextMenuStrip();

            menu.Items.Add(Tr.T("Take a break now"), null, delegate { StartBreak(); });
            menu.Items.Add(Tr.T("Reset the timer"), null, delegate { tracker.Reset(); UpdateTray(); });

            var pause = new ToolStripMenuItem(Tr.T("Pause reminders"));
            pause.DropDownItems.Add(Tr.T("For 30 minutes"), null, delegate { PauseUntil(DateTime.Now.AddMinutes(30)); });
            pause.DropDownItems.Add(Tr.T("For 1 hour"), null, delegate { PauseUntil(DateTime.Now.AddHours(1)); });
            pause.DropDownItems.Add(Tr.T("For 2 hours"), null, delegate { PauseUntil(DateTime.Now.AddHours(2)); });
            pause.DropDownItems.Add(Tr.T("Until tomorrow"), null, delegate { PauseUntil(DateTime.Today.AddDays(1)); });
            pause.DropDownItems.Add(new ToolStripSeparator());
            resumeItem = new ToolStripMenuItem(Tr.T("Resume"), null, delegate { Resume(); });
            pause.DropDownItems.Add(resumeItem);
            pause.DropDownOpening += delegate { resumeItem.Enabled = pausedUntil.HasValue; };
            menu.Items.Add(pause);

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Tr.T("Settings..."), null, delegate { OpenSettings(); });
            autostartItem = new ToolStripMenuItem(Tr.T("Start with Windows"));
            autostartItem.Click += delegate { ToggleAutostart(); };
            menu.Items.Add(autostartItem);
            menu.Items.Add(Tr.T("Support info"), null, delegate { WriteSupportInfo(); });
            menu.Items.Add(Tr.T("About"), null, delegate { OpenAbout(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Tr.T("Exit"), null, delegate { Quit(); });

            menu.Opening += delegate { autostartItem.Checked = Autostart.IsEnabled(); };
            notify.ContextMenuStrip = menu;
            if (old != null) old.Dispose();
        }

        void ToggleAutostart()
        {
            try
            {
                if (Autostart.IsEnabled()) Autostart.Disable(); else Autostart.Enable();
            }
            catch (Exception e)
            {
                Log.Error("autostart", e);
                MessageBox.Show(Tr.F("Could not change autostart:\n{0}", e.Message), Tr.T("Augensicher"));
            }
        }

        void OpenSettings()
        {
            if (settingsForm != null) { settingsForm.Activate(); return; }
            settingsForm = new SettingsForm(settings, Autostart.IsEnabled());
            settingsForm.FormClosed += delegate
            {
                var f = settingsForm;
                settingsForm = null;
                if (f.DialogResult == DialogResult.OK && f.Result != null) ApplySettings(f.Result, f.AutostartWanted);
            };
            settingsForm.Show();
            settingsForm.Activate();
        }

        void ApplySettings(Settings s, bool autostart)
        {
            try { s.Save(Settings.FilePath()); }
            catch (Exception e)
            {
                Log.Error("save settings", e);
                MessageBox.Show(Tr.F("Could not save the settings:\n{0}", e.Message), Tr.T("Augensicher"));
            }
            settings = s;
            settings.ApplyTo(tracker);
            if (autostart != Autostart.IsEnabled()) ToggleAutostart();
            Tr.SetLanguage(settings.Language);
            BuildMenu();
            UpdateTray();
            Log.Write("settings saved: work " + s.WorkMinutes + ", break " + s.BreakMinutes + ", mini " + (s.MiniBreaks ? s.MiniEveryMinutes + "/" + s.MiniSeconds + "s" : "off") + ", lang " + s.Language);
        }

        void OpenAbout()
        {
            if (aboutForm != null) { aboutForm.Activate(); return; }
            aboutForm = new AboutForm();
            aboutForm.FormClosed += delegate { aboutForm = null; };
            aboutForm.Show();
            aboutForm.Activate();
        }

        void WriteSupportInfo()
        {
            try
            {
                string path = SupportInfo.Write(dataFolder, Autostart.IsEnabled());
                SupportInfo.ShowInExplorer(path);
                MessageBox.Show(Tr.F("The support file is saved:\n\n{0}\n\nSend it to whoever is helping you. It holds the version, the Windows version, the settings and the last lines of the program's log - nothing about what you do on the computer.", path), Tr.T("Augensicher"));
            }
            catch (Exception e)
            {
                Log.Error("support info", e);
                MessageBox.Show(Tr.F("Could not write the support file:\n{0}", e.Message), Tr.T("Augensicher"));
            }
        }

        void PauseUntil(DateTime until)
        {
            pausedUntil = until;
            CloseMiniBreak();
            Log.Write("paused until " + until.ToString("yyyy-MM-dd HH:mm"));
            Balloon(Tr.F("Reminders are paused until {0}.", Clock(until)), 3000);
            UpdateTray();
        }

        void Resume()
        {
            if (!pausedUntil.HasValue) return;
            pausedUntil = null;
            Log.Write("resumed");
            Balloon(Tr.T("Reminders are back on."), 3000);
            UpdateTray();
        }

        // Today's times as 15:30, other days with the date in front.
        static string Clock(DateTime t)
        {
            return t.Date == DateTime.Today ? t.ToString("HH:mm") : t.ToString("dd.MM HH:mm");
        }

        void Quit()
        {
            Log.Write("exit");
            tickTimer.Stop();
            breakTimer.Stop();
            CloseBreakForms();
            CloseMiniBreak();
            notify.Visible = false;
            notify.Dispose();
            power.Dispose();
            ExitThread();
        }

        // ------------------------------------------------------------ ticks

        void OnTick()
        {
            DateTime now = DateTime.Now;
            double gap = (now - lastTick).TotalSeconds;
            lastTick = now;

            if (inBreak) return;   // no work is counted during a break

            if (pausedUntil.HasValue)
            {
                if (now < pausedUntil.Value) { UpdateTray(); return; }
                Resume();
            }

            var input = new TickInput
            {
                GapSeconds = gap,
                Resumed = power.TakeResumed(),
                RawIdleSeconds = Native.IdleSeconds(),
                Locked = Native.IsLocked(),
                DisplayOff = !power.DisplayOn,
                FullScreen = settings.FullScreenIsWork && Native.IsFullScreen(),
            };

            switch (tracker.Tick(input))
            {
                case TickResult.Break: StartBreak(); return;
                case TickResult.Warn: Balloon(Tr.F("Break in {0} seconds.", tracker.SecondsToBreak), 5000); break;
                case TickResult.MiniBreak: StartMiniBreak(); break;
            }
            UpdateTray();
        }

        void OnBreakTimer()
        {
            DateTime now = DateTime.Now;

            if (miniForm != null)
            {
                int left = (int)Math.Ceiling((miniEnd - now).TotalSeconds);
                if (left <= 0) CloseMiniBreak(); else miniForm.ShowState(left);
            }

            if (inBreak)
            {
                double left = (breakEnd - now).TotalSeconds;
                if (left <= 0)
                {
                    // A break that ended while the computer slept gets no sound.
                    bool sat = left > -5;
                    EndBreak(sat ? "finished" : "ended during sleep", 0);
                    if (sat && settings.Sound) SystemSounds.Asterisk.Play();
                    return;
                }
                string clock = now.ToString("HH:mm:ss");
                foreach (var f in breakForms) f.ShowState(breakTip, (int)Math.Ceiling(left), clock);
                UpdateTray();
            }

            if (!inBreak && miniForm == null) breakTimer.Stop();
        }

        // ------------------------------------------------------------ breaks

        public void StartBreak()
        {
            if (inBreak) return;
            CloseMiniBreak();
            inBreak = true;
            breakEnd = DateTime.Now.AddMinutes(settings.BreakMinutes);
            breakTip = Tr.Tips[random.Next(Tr.Tips.Length)];
            Log.Write("break start");

            BreakForm primary = null;
            foreach (var screen in Screen.AllScreens)
            {
                var f = new BreakForm(screen.Bounds, settings.AllowSkip, settings.SnoozeMinutes);
                f.SkipRequested += delegate { EndBreak("skipped", 0); };
                f.SnoozeRequested += delegate { EndBreak("snoozed", settings.SnoozeMinutes); };
                breakForms.Add(f);
                if (screen.Primary) primary = f;
            }
            foreach (var f in breakForms) f.Show();

            // Take the focus, or Esc would go to the window underneath.
            if (primary == null && breakForms.Count > 0) primary = breakForms[0];
            if (primary != null)
            {
                primary.BringToFront();
                primary.Activate();
                primary.Focus();
                primary.CatchEscapeEverywhere();
            }

            OnBreakTimer();
            breakTimer.Start();
        }

        void EndBreak(string how, int snoozeMinutes)
        {
            if (!inBreak) return;
            inBreak = false;
            CloseBreakForms();

            // The count starts over after every break, including one taken
            // from the menu; snooze leaves exactly N minutes of work.
            if (snoozeMinutes > 0) tracker.Snooze(snoozeMinutes * 60); else tracker.Reset();
            lastTick = DateTime.Now;
            Log.Write("break end: " + how);
            UpdateTray();
        }

        void CloseBreakForms()
        {
            foreach (var f in breakForms)
            {
                try { f.Hide(); f.CloseByProgram(); f.Dispose(); }
                catch (Exception e) { Log.Error("close break window", e); }
            }
            breakForms.Clear();
        }

        void StartMiniBreak()
        {
            if (miniForm != null || inBreak) return;
            miniEnd = DateTime.Now.AddSeconds(settings.MiniSeconds);
            miniForm = new MiniBreakForm();
            miniForm.FormClosed += delegate { miniForm = null; };   // Close() of a shown form disposes it
            miniForm.ShowState(settings.MiniSeconds);
            miniForm.Show();
            Log.Write("short break");
            breakTimer.Start();
        }

        void CloseMiniBreak()
        {
            if (miniForm != null) miniForm.Close();
        }

        // ------------------------------------------------------------ tray text

        string Status()
        {
            if (inBreak) return Tr.F("Break in progress: {0} left", Ui.Clock((int)Math.Ceiling((breakEnd - DateTime.Now).TotalSeconds)));
            if (pausedUntil.HasValue) return Tr.F("Paused until {0}", Clock(pausedUntil.Value));
            if (tracker.IsIdle) return Tr.F("Away, timer stopped. Break in {0}", Ui.Clock(tracker.SecondsToBreak));
            return Tr.F("Next break in {0}", Ui.Clock(tracker.SecondsToBreak));
        }

        void UpdateTray()
        {
            string text = Tr.T("Augensicher") + "\n" + Status();
            if (text.Length > 63) text = text.Substring(0, 63);   // the Windows limit for this text
            if (notify.Text != text) notify.Text = text;
        }

        string ReasonText()
        {
            switch (tracker.Reason)
            {
                case AwayReason.Locked: return Tr.T("screen locked");
                case AwayReason.DisplayOff: return Tr.T("display off");
                case AwayReason.Asleep: return Tr.T("computer was asleep");
                default: return Tr.T("no mouse or keyboard");
            }
        }

        void ShowStatusBalloon()
        {
            if (inBreak) Balloon(Tr.T("The break is on."), 3000);
            else if (pausedUntil.HasValue) Balloon(Tr.F("Reminders are paused until {0}.", Clock(pausedUntil.Value)), 4000);
            else if (tracker.IsIdle) Balloon(Tr.F("Timer stopped ({0}): {1} of work until the break.", ReasonText(), Ui.Clock(tracker.SecondsToBreak)), 4000);
            else Balloon(Tr.F("{0} of work until the break (one every {1} min at the computer).", Ui.Clock(tracker.SecondsToBreak), settings.WorkMinutes), 4000);
        }

        void Balloon(string text, int ms)
        {
            notify.ShowBalloonTip(ms, Tr.T("Augensicher"), text, ToolTipIcon.Info);
        }
    }
}
