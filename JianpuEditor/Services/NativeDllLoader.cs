using System;
using System.IO;
using System.Runtime.InteropServices;

namespace JianpuEditor.Services
{
    /// <summary>
    /// Adds the correct architecture-specific subfolder (Native\x86 or Native\x64, published as x86\ / x64\
    /// next to the exe) to the process's DLL search path, so bass.dll/bassmidi.dll resolve regardless of
    /// whether the app is running as a 32-bit or 64-bit process.
    /// </summary>
    internal static class NativeDllLoader
    {
        public static void AddNativeDependencyDirectory()
        {
            var architecture = Environment.Is64BitProcess ? "x64" : "x86";
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, architecture);
            if (!Directory.Exists(path))
            {
                AppLog.Error("Native dependency directory not found: " + path);
                return;
            }

            if (!SetDllDirectory(path))
            {
                AppLog.Error("Failed to add native dependency directory to DLL search path: " + path);
            }
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SetDllDirectory(string lpPathName);
    }
}
