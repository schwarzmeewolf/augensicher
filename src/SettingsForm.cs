using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Augensicher
{
    sealed class SettingsForm : Form
    {
        readonly NumericUpDown work, breakLen, snooze, warn, miniEvery, miniLen, idlePause, idleReset;
        readonly CheckBox allowSkip, sound, mini, fullScreen, autostart;
        readonly ComboBox language;
        readonly float scale;

        static readonly string[] LanguageCodes = { "auto", "en", "ru" };

        public Settings Result { get; private set; }
        public bool AutostartWanted { get { return autostart.Checked; } }

        public SettingsForm(Settings s, bool autostartOn)
        {
            Text = Tr.T("Augensicher") + " - " + Tr.T("Settings");
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(10);
            scale = Ui.Scale(this);
            try { Icon = Program.AppIcon(32); } catch { }

            var root = Column();
            Controls.Add(root);

            var g1 = Group(root, Tr.T("Breaks"));
            work = Number(g1, Tr.T("Work time before a break, minutes"), 1, 240, s.WorkMinutes);
            breakLen = Number(g1, Tr.T("Break length, minutes"), 1, 60, s.BreakMinutes);
            snooze = Number(g1, Tr.T("Snooze button postpones by, minutes"), 1, 60, s.SnoozeMinutes);
            warn = Number(g1, Tr.T("Warn before a break, seconds (0 = off)"), 0, 300, s.WarnSeconds);
            allowSkip = Check(g1, Tr.T("Allow skipping a break (Esc or click)"), s.AllowSkip);
            sound = Check(g1, Tr.T("Play a sound when a break ends"), s.Sound);

            var g2 = Group(root, Tr.T("Short breaks (20-20-20 rule)"));
            mini = Check(g2, Tr.T("Remind me to look into the distance"), s.MiniBreaks);
            miniEvery = Number(g2, Tr.T("Every, minutes"), 5, 120, s.MiniEveryMinutes);
            miniLen = Number(g2, Tr.T("For, seconds"), 5, 120, s.MiniSeconds);
            mini.CheckedChanged += delegate { miniEvery.Enabled = miniLen.Enabled = mini.Checked; };
            miniEvery.Enabled = miniLen.Enabled = mini.Checked;

            var g3 = Group(root, Tr.T("When you are away"));
            idlePause = Number(g3, Tr.T("No mouse or keyboard this long means away, minutes"), 1, 30, s.IdlePauseMinutes);
            idleReset = Number(g3, Tr.T("Away this long means start over, minutes"), 1, 120, s.IdleResetMinutes);
            fullScreen = Check(g3, Tr.T("Full-screen video and games count as work"), s.FullScreenIsWork);

            var g4 = Group(root, Tr.T("General"));
            language = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = S(160) };
            language.Items.AddRange(new object[] { Tr.T("Same as Windows"), "English", "Русский" });
            language.SelectedIndex = Math.Max(0, Array.IndexOf(LanguageCodes, s.Language));
            Row(g4, Tr.T("Language"), language);
            autostart = Check(g4, Tr.T("Start with Windows"), autostartOn);

            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, S(8), 0, 0) };
            var cancel = new Button { Text = Tr.T("Cancel"), DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(S(90), 0) };
            var save = new Button { Text = Tr.T("Save"), AutoSize = true, MinimumSize = new Size(S(90), 0) };
            save.Click += delegate { OnSave(); };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(save);
            root.Controls.Add(buttons);
            AcceptButton = save;
            CancelButton = cancel;
        }

        int S(int px) { return (int)(px * scale); }

        void OnSave()
        {
            var s = new Settings
            {
                WorkMinutes = (int)work.Value,
                BreakMinutes = (int)breakLen.Value,
                SnoozeMinutes = (int)snooze.Value,
                WarnSeconds = (int)warn.Value,
                AllowSkip = allowSkip.Checked,
                Sound = sound.Checked,
                MiniBreaks = mini.Checked,
                MiniEveryMinutes = (int)miniEvery.Value,
                MiniSeconds = (int)miniLen.Value,
                IdlePauseMinutes = (int)idlePause.Value,
                IdleResetMinutes = (int)idleReset.Value,
                FullScreenIsWork = fullScreen.Checked,
                Language = LanguageCodes[Math.Max(0, language.SelectedIndex)],
            };
            s.Normalize();
            Result = s;
            DialogResult = DialogResult.OK;
            Close();
        }

        static TableLayoutPanel Column()
        {
            return new TableLayoutPanel { ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill };
        }

        TableLayoutPanel Group(TableLayoutPanel root, string title)
        {
            var box = new GroupBox { Text = title, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, Padding = new Padding(S(8)), Margin = new Padding(0, 0, 0, S(6)) };
            var grid = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            box.Controls.Add(grid);
            root.Controls.Add(box);
            return grid;
        }

        void Row(TableLayoutPanel grid, string label, Control input)
        {
            var l = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, S(6), S(12), S(6)) };
            grid.Controls.Add(l);
            grid.Controls.Add(input);
        }

        NumericUpDown Number(TableLayoutPanel grid, string label, int min, int max, int value)
        {
            var n = new NumericUpDown { Minimum = min, Maximum = max, Width = S(80), Anchor = AnchorStyles.Left };
            n.Value = Math.Max(min, Math.Min(max, value));
            Row(grid, label, n);
            return n;
        }

        CheckBox Check(TableLayoutPanel grid, string label, bool value)
        {
            var c = new CheckBox { Text = label, Checked = value, AutoSize = true, Margin = new Padding(0, S(4), 0, S(4)) };
            grid.Controls.Add(c);
            grid.SetColumnSpan(c, 2);
            return c;
        }
    }

    sealed class AboutForm : Form
    {
        public AboutForm()
        {
            Text = Tr.T("About") + " - " + Tr.T("Augensicher");
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(14);
            float scale = Ui.Scale(this);
            try { Icon = Program.AppIcon(32); } catch { }

            var col = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            Controls.Add(col);

            var title = new Label { Text = AppInfo.Name, AutoSize = true, Font = new Font("Segoe UI", 16f, FontStyle.Bold) };
            col.Controls.Add(title);
            col.Controls.Add(new Label { Text = Tr.T("Eye break reminder for Windows") + "\n" + Tr.F("Version {0}", AppInfo.Version), AutoSize = true, Margin = new Padding(0, 0, 0, (int)(10 * scale)) });
            col.Controls.Add(new Label
            {
                Text = Tr.T("Free software under the MIT License.\nIt comes as is, with no warranty of any kind.\n\nNot a medical device and not medical advice. If your eyes hurt, see a doctor."),
                AutoSize = true,
                MaximumSize = new Size((int)(420 * scale), 0),
                Margin = new Padding(0, 0, 0, (int)(10 * scale)),
            });

            col.Controls.Add(Link(Tr.T("Project page"), AppInfo.Url));
            col.Controls.Add(Link(Tr.T("License"), LocalOrWeb("LICENSE.txt", "/blob/main/LICENSE")));
            col.Controls.Add(Link(Tr.T("Disclaimer"), LocalOrWeb("DISCLAIMER.md", "/blob/main/DISCLAIMER.md")));

            var close = new Button { Text = Tr.T("Close"), DialogResult = DialogResult.OK, AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(0, (int)(12 * scale), 0, 0) };
            col.Controls.Add(close);
            AcceptButton = close;
            CancelButton = close;
        }

        // The archive carries LICENSE.txt and DISCLAIMER.md next to the exe;
        // a bare exe falls back to the copies on the project page.
        static string LocalOrWeb(string file, string webPath)
        {
            string local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file);
            return File.Exists(local) ? local : AppInfo.Url + webPath;
        }

        static LinkLabel Link(string text, string target)
        {
            var l = new LinkLabel { Text = text, AutoSize = true, Margin = new Padding(0, 2, 0, 2) };
            l.LinkClicked += delegate
            {
                try
                {
                    if (!target.StartsWith("http")) Process.Start("notepad.exe", "\"" + target + "\"");
                    else Process.Start(target);
                }
                catch (Exception e) { Log.Error("About link", e); }
            };
            return l;
        }
    }
}
