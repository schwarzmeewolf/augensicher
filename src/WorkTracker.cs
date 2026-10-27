using System;

namespace Augensicher
{
    public enum TickResult { None, Warn, MiniBreak, Break }

    public enum AwayReason { None, Locked, DisplayOff, NoInput, Asleep }

    // What Windows said about the last second. Filled by the app from the
    // system, and by the tests by hand.
    public struct TickInput
    {
        public double GapSeconds;     // time since the previous tick
        public bool Resumed;          // Windows reported a wake from sleep
        public int RawIdleSeconds;    // seconds since the last mouse or key input
        public bool Locked;           // Win+L, the sign-in screen, a UAC prompt
        public bool DisplayOff;
        public bool FullScreen;       // the foreground window covers its monitor
    }

    // Counts time actually spent at the computer and says when a break is due.
    //
    // Called once a second. Holds no timers and touches no Windows API, so the
    // tests drive it directly. Not called during a break or a pause.
    public sealed class WorkTracker
    {
        public int WorkSeconds = 45 * 60;      // work needed before a break
        public int IdlePauseSeconds = 2 * 60;  // no input this long = away, the count stops
        public int IdleResetSeconds = 5 * 60;  // away this long = eyes rested, start over
        public int WarnSeconds = 30;           // warn this long before a break, 0 = never
        public int MiniEverySeconds = 0;       // short break every N seconds of work, 0 = off
        public bool FullScreenIsWork = true;   // a full-screen video keeps counting without input

        // A short break this close to the long one is dropped: the long one covers it.
        public const int MiniBreakGuardSeconds = 120;
        // More than this between two ticks means the computer was asleep.
        public const double SleepGapSeconds = 10;

        public int Worked { get; private set; }
        public int MiniWorked { get; private set; }
        public int IdleSeconds { get; private set; }
        public bool IsIdle { get; private set; }
        public AwayReason Reason { get; private set; }
        bool warned;

        public int SecondsToBreak { get { return Math.Max(0, WorkSeconds - Worked); } }

        public TickResult Tick(TickInput input)
        {
            if (input.GapSeconds > SleepGapSeconds || input.Resumed)
            {
                IdleSeconds += (int)Math.Max(input.GapSeconds, 0);
                IsIdle = true;
                Reason = AwayReason.Asleep;
                if (IdleSeconds >= IdleResetSeconds) StartOver();
                return TickResult.None;
            }

            bool wasIdle = IsIdle;
            IsIdle = Away(input);

            if (IsIdle)
            {
                // The threshold itself was counted as work before we knew the
                // person had left; give it back at the moment of leaving.
                if (!wasIdle)
                {
                    int back = Math.Min(IdleSeconds, IdlePauseSeconds);
                    Worked = Math.Max(0, Worked - back);
                    MiniWorked = Math.Max(0, MiniWorked - back);
                }
                if (IdleSeconds >= IdleResetSeconds) StartOver();
                return TickResult.None;
            }

            Worked++;
            MiniWorked++;

            if (Worked >= WorkSeconds) return TickResult.Break;

            if (WarnSeconds > 0 && !warned && WorkSeconds > WarnSeconds && WorkSeconds - Worked <= WarnSeconds)
            {
                warned = true;
                return TickResult.Warn;
            }

            if (MiniEverySeconds > 0 && MiniWorked >= MiniEverySeconds)
            {
                MiniWorked = 0;
                if (WorkSeconds - Worked > MiniBreakGuardSeconds) return TickResult.MiniBreak;
            }

            return TickResult.None;
        }

        bool Away(TickInput input)
        {
            // Locked or dark screen: away at once, without waiting for the threshold.
            if (input.Locked || input.DisplayOff)
            {
                Reason = input.Locked ? AwayReason.Locked : AwayReason.DisplayOff;
                IdleSeconds = Math.Max(IdleSeconds + 1, input.RawIdleSeconds);
                return true;
            }

            // A film or a game: the eyes are working even when the mouse is still.
            if (FullScreenIsWork && input.FullScreen)
            {
                Reason = AwayReason.None;
                IdleSeconds = 0;
                return false;
            }

            if (input.RawIdleSeconds >= IdlePauseSeconds)
            {
                Reason = AwayReason.NoInput;
                IdleSeconds = input.RawIdleSeconds;   // already includes the threshold
                return true;
            }

            Reason = AwayReason.None;
            IdleSeconds = 0;
            return false;
        }

        void StartOver()
        {
            Worked = 0;
            MiniWorked = 0;
            warned = false;
        }

        // After a break, and from the "Reset the timer" menu item.
        public void Reset()
        {
            StartOver();
            IdleSeconds = 0;
            IsIdle = false;
            Reason = AwayReason.None;
        }

        // "Snooze": leave exactly this much work until the next break.
        public void Snooze(int seconds)
        {
            Worked = Math.Max(0, WorkSeconds - seconds);
            warned = false;
            IdleSeconds = 0;
            IsIdle = false;
            Reason = AwayReason.None;
        }
    }
}
