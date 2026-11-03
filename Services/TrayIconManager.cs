using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace MultiTeam.Services
{
    public class TrayIconManager : IDisposable
    {
        private const int WM_USER = 0x0400;
        private const int WM_TRAYICON = WM_USER + 101;

        private const int NIM_ADD = 0x00000000;
        private const int NIM_MODIFY = 0x00000001;
        private const int NIM_DELETE = 0x00000002;

        private const int NIF_MESSAGE = 0x00000001;
        private const int NIF_ICON = 0x00000002;
        private const int NIF_TIP = 0x00000004;

        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONUP = 0x0205;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public int uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public int dwState;
            public int dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public int uTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public int dwInfoFlags;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int ExtractIconEx(string lpszFile, int nIconIndex, out IntPtr phiconLarge, out IntPtr phiconSmall, int nIcons);

        [DllImport("user32.dll")]
        private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private readonly Window _window;
        private readonly Action _onRestore;
        private readonly Action _onExit;
        private HwndSource? _hwndSource;
        private bool _isAdded;
        private IntPtr _customHIcon = IntPtr.Zero;
        private const int TRAY_ID = 9001;

        public TrayIconManager(Window window, Action onRestore, Action onExit)
        {
            _window = window;
            _onRestore = onRestore;
            _onExit = onExit;

            _window.Loaded += Window_Loaded;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var helper = new WindowInteropHelper(_window);
            _hwndSource = HwndSource.FromHwnd(helper.Handle);
            _hwndSource?.AddHook(WndProc);

            AddTrayIcon();
        }

        private void AddTrayIcon()
        {
            var helper = new WindowInteropHelper(_window);
            if (helper.Handle == IntPtr.Zero) return;

            IntPtr hIcon = IntPtr.Zero;

            // 1. Try loading app.ico from local directory
            string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (System.IO.File.Exists(iconPath))
            {
                hIcon = LoadImage(IntPtr.Zero, iconPath, 1 /* IMAGE_ICON */, 16, 16, 0x00000010 /* LR_LOADFROMFILE */);
            }

            // 2. Fallback to extracting from executable PE header
            if (hIcon == IntPtr.Zero)
            {
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath) && System.IO.File.Exists(exePath))
                {
                    ExtractIconEx(exePath, 0, out _, out hIcon, 1);
                }
            }

            // 3. Fallback to default application icon
            if (hIcon == IntPtr.Zero)
            {
                hIcon = LoadIcon(IntPtr.Zero, (IntPtr)32512); // IDI_APPLICATION
            }
            else
            {
                _customHIcon = hIcon;
            }

            var data = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = helper.Handle,
                uID = TRAY_ID,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = hIcon,
                szTip = "MultiTeam - Trình Quản Lý Chat Đa Tài Khoản"
            };

            Shell_NotifyIcon(NIM_ADD, ref data);
            _isAdded = true;
        }

        public void UpdateTooltip(string tooltip)
        {
            if (!_isAdded) return;
            var helper = new WindowInteropHelper(_window);
            if (helper.Handle == IntPtr.Zero) return;

            var data = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = helper.Handle,
                uID = TRAY_ID,
                uFlags = NIF_TIP,
                szTip = tooltip.Length > 120 ? tooltip.Substring(0, 120) : tooltip
            };

            Shell_NotifyIcon(NIM_MODIFY, ref data);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == (int)App.WM_SHOW_MULTITEAM)
            {
                _onRestore();
                handled = true;
                return IntPtr.Zero;
            }

            if (msg == WM_TRAYICON)
            {
                long mouseMsg = lParam.ToInt64() & 0xFFFF;
                if (mouseMsg == WM_LBUTTONDBLCLK || mouseMsg == WM_LBUTTONUP)
                {
                    _onRestore();
                    handled = true;
                }
                else if (mouseMsg == WM_RBUTTONUP)
                {
                    ShowTrayMenu();
                    handled = true;
                }
            }

            return IntPtr.Zero;
        }

        private void ShowTrayMenu()
        {
            var menu = new ContextMenu
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 41, 59)),
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 250, 252)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 65, 85))
            };

            var itemOpen = new MenuItem { Header = "Mở MultiTeam", FontWeight = FontWeights.Bold };
            itemOpen.Click += (s, e) => _onRestore();
            menu.Items.Add(itemOpen);

            menu.Items.Add(new Separator { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 65, 85)) });

            var itemExit = new MenuItem { Header = "Thoát ứng dụng" };
            itemExit.Click += (s, e) => _onExit();
            menu.Items.Add(itemExit);

            menu.IsOpen = true;
        }

        public void RemoveTrayIcon()
        {
            if (!_isAdded) return;

            var helper = new WindowInteropHelper(_window);
            if (helper.Handle != IntPtr.Zero)
            {
                var data = new NOTIFYICONDATA
                {
                    cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                    hWnd = helper.Handle,
                    uID = TRAY_ID
                };
                Shell_NotifyIcon(NIM_DELETE, ref data);
            }
            _isAdded = false;
        }

        public void Dispose()
        {
            RemoveTrayIcon();
            if (_customHIcon != IntPtr.Zero)
            {
                DestroyIcon(_customHIcon);
                _customHIcon = IntPtr.Zero;
            }
            _hwndSource?.RemoveHook(WndProc);
            _hwndSource?.Dispose();
        }
    }
}
