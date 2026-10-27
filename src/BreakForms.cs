using System;
using System.Drawing;
using System.Windows.Forms;

namespace Augensicher
{
    static class Ui
    {
        public static readonly Color Background = Color.FromArgb(18, 22, 38);
        public static readonly Color Dim = Color.FromArgb(120, 140, 170);
        public static readonly Color ClockColor = Color.FromArgb(180, 200, 230);
        public static readonly Color ButtonBack = Color.FromArgb(45, 55, 85);
        public static readonly Color ButtonBorder = Color.FromArgb(90, 110, 150);

        // Pixel sizes below are written for 100% scaling; this makes them
        // right at 125%, 150% and so on. Fonts in points scale by themselves.
        public static float Scale(Control c)
        {
            using (var g = c.CreateGraphics()) return g.DpiX / 96f;
        }

        public static string Clock(int seconds)
        {
            if (seconds < 0) seconds = 0;
            return string.Format("{0}:{1:D2}", seconds / 60, seconds % 60);
        }
    }

    // The long break: one dark window on every monitor.
    sealed class BreakForm : Form
    {
        readonly Label message = new Label();
        readonly Label clock = new Label();
        readonly Label version = new Label();
        readonly Button snooze = new Button();
        readonly bool allowSkip;
        readonly float scale;
        bool closingByProgram;

        public event EventHandler SkipRequested;
        public event EventHandler SnoozeRequested;

        public BreakForm(Rectangle bounds, bool allowSkip, int snoozeMinutes)
        {
            this.allowSkip = allowSkip;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;
            BackColor = Ui.Background;
            TopMost = true;
            ShowInTaskbar = false;
            KeyPreview = true;
            Text = Tr.T("Eye break");
            if (allowSkip) Cursor = Cursors.Hand;
            scale = Ui.Scale(this);

            message.TextAlign = ContentAlignment.MiddleCenter;
            message.ForeColor = Color.White;
            message.Font = new Font("Segoe UI", 26f);
            message.Click += delegate { Skip(); };
            Controls.Add(message);

            clock.TextAlign = ContentAlignment.MiddleRight;
            clock.ForeColor = Ui.ClockColor;
            clock.BackColor = Color.Transparent;
            clock.Font = new Font("Segoe UI", 22f, FontStyle.Bold);
            Controls.Add(clock);
            clock.BringToFront();

            version.TextAlign = ContentAlignment.MiddleLeft;
            version.ForeColor = Ui.Dim;
            version.BackColor = Color.Transparent;
            version.Font = new Font("Segoe UI", 12f);
            version.Text = Tr.T("Augensicher") + "  v" + AppInfo.Version;
            Controls.Add(version);
            version.BringToFront();

            snooze.Text = Tr.F("Snooze for {0} min", snoozeMinutes);
            snooze.Font = new Font("Segoe UI", 14f);
            snooze.FlatStyle = FlatStyle.Flat;
            snooze.TabStop = false;   // the form keeps the focus, so Esc works at once
            snooze.ForeColor = Color.White;
            snooze.BackColor = Ui.ButtonBack;
            snooze.FlatAppearance.BorderColor = Ui.ButtonBorder;
            snooze.Cursor = Cursors.Hand;
            snooze.Click += delegate { if (SnoozeRequested != null) SnoozeRequested(this, EventArgs.Empty); };
            Controls.Add(snooze);
            snooze.BringToFront();

            Click += delegate { Skip(); };
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Skip(); };
            PlaceControls();
        }

        void Skip()
        {
            if (allowSkip && SkipRequested != null) SkipRequested(this, EventArgs.Empty);
        }

        int S(int px) { return (int)(px * scale); }

        void PlaceControls()
        {
            var size = ClientSize;
            // The message gets the middle band only: overlapping labels hide
            // each other, depending on the order Windows paints them in.
            message.SetBounds(0, S(100), size.Width, Math.Max(S(100), size.Height - S(100) - S(190)));
            clock.SetBounds(size.Width - S(320), S(25), S(290), S(60));
            version.SetBounds(S(25), size.Height - S(55), S(400), S(40));
            int w = Math.Max(S(300), TextRenderer.MeasureText(snooze.Text, snooze.Font).Width + S(40));
            snooze.SetBounds((size.Width - w) / 2, size.Height - S(170), w, S(52));
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (snooze != null) PlaceControls();
        }

        public void ShowState(string tip, int secondsLeft, string clockText)
        {
            string text = Tr.T("Eye break") + "\n\n" + tip + "\n\n" + Tr.F("Time left: {0}", Ui.Clock(secondsLeft));
            if (allowSkip) text += "\n\n" + Tr.T("(press Esc or click anywhere to skip)");
            if (message.Text != text) message.Text = text;
            if (clock.Text != clockText) clock.Text = clockText;
        }

        const int WM_HOTKEY = 0x0312;
        const int EscapeHotKeyId = 1;
        const uint VK_ESCAPE = 0x1B;
        bool escapeRegistered;

        // Windows does not let a background program take the keyboard focus
        // from the window the person is typing in, so a break that starts on
        // its own may not get the focus, and Esc would go to that window.
        // Holding Esc as a system-wide hot key for the length of the break
        // makes it work wherever the focus is.
        public void CatchEscapeEverywhere()
        {
            if (allowSkip && !escapeRegistered)
                escapeRegistered = Native.RegisterHotKey(Handle, EscapeHotKeyId, 0, VK_ESCAPE);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == EscapeHotKeyId) Skip();
            base.WndProc(ref m);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (escapeRegistered) { Native.UnregisterHotKey(Handle, EscapeHotKeyId); escapeRegistered = false; }
            base.OnHandleDestroyed(e);
        }

        public void CloseByProgram()
        {
            closingByProgram = true;
            Close();
        }

        // Alt+F4 is a skip like any other, and not allowed when skipping is off.
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!closingByProgram && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Skip();
            }
            base.OnFormClosing(e);
        }
    }

    // The short 20-20-20 break: a small window that does not take the focus,
    // so typing goes on undisturbed if the person ignores it.
    sealed class MiniBreakForm : Form
    {
        readonly Label text = new Label();

        public MiniBreakForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            BackColor = Ui.Background;
            TopMost = true;
            ShowInTaskbar = false;
            Text = Tr.T("Short break");
            float scale = Ui.Scale(this);
            Size = new Size((int)(480 * scale), (int)(170 * scale));
            var area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 3);

            text.Dock = DockStyle.Fill;
            text.TextAlign = ContentAlignment.MiddleCenter;
            text.ForeColor = Color.White;
            text.Font = new Font("Segoe UI", 16f);
            text.Cursor = Cursors.Hand;
            text.Click += delegate { Close(); };
            Controls.Add(text);
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_NOACTIVATE = 0x08000000, WS_EX_TOOLWINDOW = 0x00000080, WS_EX_TOPMOST = 0x00000008;
                var p = base.CreateParams;
                p.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
                return p;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Ui.ButtonBorder, 2)) e.Graphics.DrawRectangle(pen, 1, 1, Width - 2, Height - 2);
        }

        public void ShowState(int secondsLeft)
        {
            string t = Tr.T("Short break") + "\n" + Tr.T("Look at something far away") + "\n" + Ui.Clock(secondsLeft);
            if (text.Text != t) text.Text = t;
        }
    }
}
