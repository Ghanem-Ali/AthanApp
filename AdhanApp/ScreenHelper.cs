using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;

namespace AdhanApp
{
    public class ScreenInfo
    {
        public Rect WorkingArea { get; set; }
        public bool Primary { get; set; }
    }

    public static class ScreenHelper
    {
        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        private const int MONITORINFOF_PRIMARY = 0x0001;

        public static List<ScreenInfo> AllScreens()
        {
            var screens = new List<ScreenInfo>();

            MonitorEnumProc callback = (IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData) =>
            {
                MONITORINFOEX mi = new MONITORINFOEX();
                mi.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
                if (GetMonitorInfo(hMonitor, ref mi))
                {
                    screens.Add(new ScreenInfo
                    {
                        WorkingArea = new Rect(
                            mi.rcWork.Left, 
                            mi.rcWork.Top, 
                            Math.Max(0, mi.rcWork.Right - mi.rcWork.Left), 
                            Math.Max(0, mi.rcWork.Bottom - mi.rcWork.Top)),
                        Primary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0
                    });
                }
                return true;
            };

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);

            return screens;
        }
    }
}
