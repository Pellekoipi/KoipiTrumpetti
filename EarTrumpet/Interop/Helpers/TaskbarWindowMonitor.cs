using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Windows.Threading;

namespace EarTrumpet.Interop.Helpers
{
    /// <summary>
    /// Tracks processes that own a taskbar button and maps an audio-session process
    /// onto an ancestor that owns one. Browsers and Discord often play audio from a
    /// child process while the visible window belongs to the parent.
    /// </summary>
    public sealed class TaskbarWindowMonitor
    {
        public static TaskbarWindowMonitor Current => _current ?? (_current = new TaskbarWindowMonitor());

        public event EventHandler Changed;

        private const int WS_EX_APPWINDOW = 0x00040000;
        private const uint GW_OWNER = 4;
        private const uint TH32CS_SNAPPROCESS = 0x00000002;
        private const int MaxWalkDepth = 24;

        private static readonly HashSet<string> ProcessTreeStops = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "explorer.exe",
            "sihost.exe",
            "svchost.exe",
            "services.exe",
            "winlogon.exe",
            "lsass.exe",
            "csrss.exe",
            "dwm.exe",
            "smss.exe",
            "wininit.exe",
            "taskhostw.exe",
            "runtimebroker.exe",
            "applicationframehost.exe",
            "searchhost.exe",
            "startmenuexperiencehost.exe",
            "shellexperiencehost.exe",
            "textinputhost.exe",
            "systemsettings.exe",
            "lockapp.exe",
            "fontdrvhost.exe",
            "dashost.exe",
            "ctfmon.exe",
        };

        private static TaskbarWindowMonitor _current;

        private readonly Win32Window _window;
        private readonly int _shellMessage;
        private readonly DispatcherTimer _debounce;
        private readonly DispatcherTimer _poll;
        private readonly Delegate _enumProc;
        private HashSet<int> _taskbarPids = new HashSet<int>();
        private Dictionary<int, int> _parents = new Dictionary<int, int>();
        private Dictionary<int, string> _imageNames = new Dictionary<int, string>();
        private bool _refreshing;

        private TaskbarWindowMonitor()
        {
            _enumProc = (EnumWindowsProc)OnEnumWindow;
            _window = new Win32Window();
            _window.Initialize(OnWndProc);
            _shellMessage = User32.RegisterWindowMessage("SHELLHOOK");
            RegisterShellHookWindow(_window.Handle);

            _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _debounce.Tick += (_, __) =>
            {
                _debounce.Stop();
                Refresh();
            };

            _poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
            _poll.Tick += (_, __) => Refresh();
            _poll.Start();
            Refresh();
        }

        public bool HasTaskbarWindow(int processId)
        {
            if (processId <= 4)
            {
                return false;
            }

            var taskbarPids = _taskbarPids;
            var parents = _parents;
            var names = _imageNames;
            var seen = new HashSet<int>();
            var current = processId;

            for (var depth = 0; depth < MaxWalkDepth && current > 4; depth++)
            {
                if (!seen.Add(current))
                {
                    return false;
                }

                if (taskbarPids.Contains(current))
                {
                    return true;
                }

                int parent;
                if (!parents.TryGetValue(current, out parent) || parent == 0 || parent == current)
                {
                    return false;
                }

                string parentName;
                if (names.TryGetValue(parent, out parentName) && ProcessTreeStops.Contains(parentName))
                {
                    return false;
                }

                current = parent;
            }

            return false;
        }

        public void Refresh()
        {
            if (_refreshing)
            {
                return;
            }

            _refreshing = true;
            try
            {
                _pendingPids = new HashSet<int>();
                EnumWindows((EnumWindowsProc)_enumProc, IntPtr.Zero);
                var pids = _pendingPids;
                _pendingPids = null;

                var parents = new Dictionary<int, int>();
                var names = new Dictionary<int, string>();
                SnapshotProcesses(parents, names);

                var changed = !pids.SetEquals(_taskbarPids) || !ParentsEqual(parents, _parents);
                _taskbarPids = pids;
                _parents = parents;
                _imageNames = names;

                if (changed)
                {
                    Changed?.Invoke(this, EventArgs.Empty);
                }
            }
            finally
            {
                _refreshing = false;
            }
        }

        private HashSet<int> _pendingPids;

        private bool OnEnumWindow(IntPtr hwnd, IntPtr lParam)
        {
            if (_pendingPids == null)
            {
                return true;
            }

            if (IsTaskbarWindow(hwnd))
            {
                uint pid;
                User32.GetWindowThreadProcessId(hwnd, out pid);
                if (pid > 4)
                {
                    _pendingPids.Add(unchecked((int)pid));
                }
            }

            return true;
        }

        private void OnWndProc(Message message)
        {
            if (message.Msg == _shellMessage)
            {
                ScheduleRefresh();
            }
        }

        private void ScheduleRefresh()
        {
            _debounce.Stop();
            _debounce.Start();
        }

        private static bool ParentsEqual(Dictionary<int, int> next, Dictionary<int, int> previous)
        {
            if (next.Count != previous.Count)
            {
                return false;
            }

            foreach (var pair in next)
            {
                int parent;
                if (!previous.TryGetValue(pair.Key, out parent) || parent != pair.Value)
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool IsTaskbarWindowStyle(int exStyle, bool hasOwner, bool visible, bool cloaked)
        {
            if (!visible || cloaked)
            {
                return false;
            }

            var isToolWindow = (exStyle & User32.WS_EX_TOOLWINDOW) != 0;
            var isAppWindow = (exStyle & WS_EX_APPWINDOW) != 0;
            if (isToolWindow && !isAppWindow)
            {
                return false;
            }

            if (hasOwner && !isAppWindow)
            {
                return false;
            }

            return true;
        }

        private static bool IsTaskbarWindow(IntPtr hwnd)
        {
            if (IsShellChromeWindow(hwnd))
            {
                return false;
            }

            var exStyle = User32.GetWindowLong(hwnd, User32.GWL.GWL_EXSTYLE);
            var owner = GetWindow(hwnd, GW_OWNER);
            return IsTaskbarWindowStyle(exStyle, owner != IntPtr.Zero, IsWindowVisible(hwnd), IsCloaked(hwnd));
        }

        private static bool IsShellChromeWindow(IntPtr hwnd)
        {
            var className = new StringBuilder(User32.MAX_CLASSNAME_LENGTH);
            if (User32.GetClassName(hwnd, className, className.Capacity) == 0)
            {
                return false;
            }

            var name = className.ToString();
            return name == "Shell_TrayWnd"
                || name == "Shell_SecondaryTrayWnd"
                || name == "Progman"
                || name == "WorkerW"
                || name == "NotifyIconOverflowWindow";
        }

        private static bool IsCloaked(IntPtr hwnd)
        {
            int cloaked;
            var hr = DwmApi.DwmGetWindowAttribute(hwnd, DwmApi.DWMWA_CLOAKED, out cloaked, Marshal.SizeOf(typeof(int)));
            return hr == 0 && cloaked != 0;
        }

        private static void SnapshotProcesses(Dictionary<int, int> parents, Dictionary<int, string> names)
        {
            var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
            if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
            {
                return;
            }

            try
            {
                var entry = new PROCESSENTRY32();
                entry.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));
                if (!Process32First(snapshot, ref entry))
                {
                    return;
                }

                do
                {
                    var pid = unchecked((int)entry.th32ProcessID);
                    parents[pid] = unchecked((int)entry.th32ParentProcessID);
                    if (!string.IsNullOrEmpty(entry.szExeFile))
                    {
                        names[pid] = entry.szExeFile;
                    }

                    entry.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));
                }
                while (Process32Next(snapshot, ref entry));
            }
            finally
            {
                Kernel32.CloseHandle(snapshot);
            }
        }

        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct PROCESSENTRY32
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RegisterShellHookWindow(IntPtr hWnd);

        [DllImport("user32.dll", PreserveSig = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", PreserveSig = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", PreserveSig = true)]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("kernel32.dll", SetLastError = true, PreserveSig = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true, PreserveSig = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true, PreserveSig = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);
    }
}
