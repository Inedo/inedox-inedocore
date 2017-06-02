using System;
using System.IO;

namespace Inedo.Extensions.UniversalPackages
{
    internal static class Remote
    {
        public static string GetMachineRegistryRoot() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "upack");
        public static string GetCurrentUserRegistryRoot() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".upack");
    }
}
