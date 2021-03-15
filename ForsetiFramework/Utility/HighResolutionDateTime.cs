using System;
using System.Runtime.InteropServices;

namespace ForsetiFramework.Utility
{
    public static class HighResolutionDateTime
    {
        public static bool IsAvailable { get; private set; }

        [DllImport("Kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        private static extern void GetSystemTimePreciseAsFileTime(out long filetime);

        public static DateTime UtcNow
        {
            get
            {
                if (!IsAvailable)
                {
                    throw new InvalidOperationException(
                        "High resolution clock isn't available.");
                }

                GetSystemTimePreciseAsFileTime(out var filetime);
                return DateTime.FromFileTimeUtc(filetime);
            }
        }

        static HighResolutionDateTime()
        {
            try
            {
                GetSystemTimePreciseAsFileTime(out _);
                IsAvailable = true;
            }
            catch (EntryPointNotFoundException)
            {
                // Not running Windows 8 or higher.
                IsAvailable = false;
            }
        }
    }
}
