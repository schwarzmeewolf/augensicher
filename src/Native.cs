using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows.Forms;

namespace Augensicher
{
    static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

        [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
        [DllImport("user32.dll", SetLastError = true)] static extern IntPtr OpenInputDesktop(uint dwFlags, bool fInherit, uint dwDesiredAccess);
        [DllImport("user32.dll", SetLastError = true)] static extern bool CloseDesktop(IntPtr hDesktop);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        // Seconds since the last mouse or keyboard input, in any program.
        public static int IdleSeconds()
        {
            var lii = new LASTINPUTINFO();
            lii.cbSize = (uint)Marshal.SizeOf(lii);
            if (!GetLastInputInfo(ref lii)) return 0;
            return (int)(((uint)Environment.TickCount - lii.dwTime) / 1000);
        }

        // Win+L, the sign-in screen or a UAC prompt: our desktop is not the input one.
        public static bool IsLocked()
        {
            const uint DESKTOP_SWITCHDESKTOP = 0x0100;
            IntPtr h = OpenInputDesktop(0, false, DESKTOP_SWITCHDESKTOP);
            if (h == IntPtr.Zero) return true;
            CloseDesktop(h);
            return false;
        }

        // The foreground window covers its whole monitor: a video, a game, a slideshow.
        public static bool IsFullScreen()
        {
            IntPtr h = GetForegroundWindow();
            if (h == IntPtr.Zero) return false;

            // The desktop and the taskbar cover the screen too, but nobody is watching them.
            var sb = new StringBuilder(64);
            GetClassName(h, sb, sb.Capacity);
            string cls = sb.ToString();
            if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Windows.UI.Core.CoreWindow")
                return false;

            RECT r;
            if (!GetWindowRect(h, out r)) return false;
            var b = Screen.FromHandle(h).Bounds;
            return r.Left <= b.Left && r.Top <= b.Top && r.Right >= b.Right && r.Bottom >= b.Bottom;
        }
    }

    // Listens to Windows for the display turning off and the computer waking up.
    sealed class PowerWatcher : NativeWindow, IDisposable
    {
        const int WM_POWERBROADCAST = 0x0218;
        const int PBT_POWERSETTINGCHANGE = 0x8013;
        const int PBT_APMRESUMESUSPEND = 0x0007;
        const int PBT_APMRESUMEAUTOMATIC = 0x0012;
        static readonly Guid GUID_CONSOLE_DISPLAY_STATE = new Guid("6fe69556-704a-47a0-8f24-c28d936fda47");

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr RegisterPowerSettingNotification(IntPtr hRecipient, ref Guid PowerSettingGuid, int Flags);
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UnregisterPowerSettingNotification(IntPtr Handle);

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        struct POWERBROADCAST_SETTING { public Guid PowerSetting; public uint DataLength; public byte Data; }

        IntPtr registration = IntPtr.Zero;
        bool resumed;

        public bool DisplayOn = true;

        public PowerWatcher()
        {
            CreateHandle(new CreateParams());
            Guid g = GUID_CONSOLE_DISPLAY_STATE;
            registration = RegisterPowerSettingNotification(Handle, ref g, 0);
        }

        // True once after each wake from sleep.
        public bool TakeResumed()
        {
            bool r = resumed;
            resumed = false;
            return r;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_POWERBROADCAST)
            {
                int ev = m.WParam.ToInt32();
                if (ev == PBT_POWERSETTINGCHANGE && m.LParam != IntPtr.Zero)
                {
                    var s = (POWERBROADCAST_SETTING)Marshal.PtrToStructure(m.LParam, typeof(POWERBROADCAST_SETTING));
                    if (s.PowerSetting == GUID_CONSOLE_DISPLAY_STATE) DisplayOn = s.Data != 0;   // 0 = off, 2 = dimmed
                }
                else if (ev == PBT_APMRESUMESUSPEND || ev == PBT_APMRESUMEAUTOMATIC)
                {
                    resumed = true;
                }
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (registration != IntPtr.Zero) { UnregisterPowerSettingNotification(registration); registration = IntPtr.Zero; }
            if (Handle != IntPtr.Zero) DestroyHandle();
        }
    }

    // Writing a .lnk through the Windows shell itself. Not through a .vbs or
    // WScript.Shell: Windows 11 24H2 can come without VBScript, and a startup
    // entry that depends on it then silently does nothing.
    static class ShellLink
    {
        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        class CShellLink { }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
        interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, int fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
            void Resolve(IntPtr hwnd, int fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        public static void Create(string linkPath, string target, string description)
        {
            var link = (IShellLinkW)new CShellLink();
            link.SetPath(target);
            link.SetWorkingDirectory(System.IO.Path.GetDirectoryName(target));
            link.SetDescription(description);
            link.SetIconLocation(target, 0);
            ((IPersistFile)link).Save(linkPath, true);
            Marshal.ReleaseComObject(link);
        }

        public static string GetTarget(string linkPath)
        {
            var link = (IShellLinkW)new CShellLink();
            try
            {
                ((IPersistFile)link).Load(linkPath, 0);
                var sb = new StringBuilder(1024);
                link.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
                return sb.ToString();
            }
            finally { Marshal.ReleaseComObject(link); }
        }
    }
}
