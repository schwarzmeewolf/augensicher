using System;
using System.IO;
using System.Text;

namespace Augensicher
{
    // The program has no window to print to, so anything that goes wrong is
    // written here; otherwise a failure leaves no trace at all.
    //
    // Only the program's own events: start, settings, breaks, errors. Never
    // window titles or anything else about what the person is doing.
    static class Log
    {
        const long MaxBytes = 512 * 1024;   // past this the log starts over, keeping one old copy as .1
        static readonly object gate = new object();
        static string path;

        public static string FilePath { get { return path; } }

        public static void Init(string folder)
        {
            path = Path.Combine(folder, "augensicher.log");
        }

        public static void Write(string message)
        {
            if (path == null) return;
            lock (gate)
            {
                try
                {
                    var info = new FileInfo(path);
                    if (info.Exists && info.Length > MaxBytes)
                    {
                        string old = path + ".1";
                        if (File.Exists(old)) File.Delete(old);
                        File.Move(path, old);
                    }
                    File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine, new UTF8Encoding(false));
                }
                catch { }   // a log that cannot be written must not take the program down
            }
        }

        public static void Error(string where, Exception e)
        {
            Write("ERROR in " + where + ": " + e);
        }
    }
}
