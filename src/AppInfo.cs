using System.Reflection;

// The version lives here and only here: the assembly attributes, the About
// window, the support file and tools\build.ps1 (archive name) all read it.
[assembly: AssemblyTitle(Augensicher.AppInfo.Name)]
[assembly: AssemblyProduct(Augensicher.AppInfo.Name)]
[assembly: AssemblyDescription("Eye break reminder for Windows")]
[assembly: AssemblyCopyright("Copyright (c) 2026 schwarzmeewolf. MIT License.")]
[assembly: AssemblyVersion(Augensicher.AppInfo.Version + ".0")]
[assembly: AssemblyFileVersion(Augensicher.AppInfo.Version + ".0")]

namespace Augensicher
{
    static class AppInfo
    {
        public const string Name = "Augensicher";
        public const string Version = "2.0.0";
        public const string Url = "https://github.com/schwarzmeewolf/augensicher";
    }
}
