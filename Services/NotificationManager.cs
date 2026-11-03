using System;
using System.Collections.Generic;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using MultiTeam.Models;
using MultiTeam.Views;

namespace MultiTeam.Services
{
    public class NotificationManager
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        [StructLayout(LayoutKind.Sequential)]
        private struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        private const uint FLASHW_ALL = 3;
        private const uint FLASHW_TIMERNOFG = 12;

        private readonly Window _mainWindow;
        private readonly Action<TeamAccount> _onAccountSelect;
        private readonly Func<string, bool> _isAccountActive;
        private readonly Dictionary<string, int> _lastUnreadCounts = new();
        private NotificationToastWindow? _currentToast;

        public NotificationManager(Window mainWindow, Action<TeamAccount> onAccountSelect, Func<string, bool> isAccountActive)
        {
            _mainWindow = mainWindow;
            _onAccountSelect = onAccountSelect;
            _isAccountActive = isAccountActive;
        }

        public void HandleNotificationReceived(TeamAccount account, string title, string body)
        {
            if (account.IsMuted)
            {
                return;
            }

            _mainWindow.Dispatcher.Invoke(() =>
            {
                // Suppress toast if user is actively viewing and focused on this chat
                if (_mainWindow.IsActive && _mainWindow.WindowState != WindowState.Minimized && _isAccountActive(account.Id))
                {
                    return;
                }

                if (account.UnreadCount == 0)
                {
                    account.UnreadCount = 1;
                }

                TriggerAlert(account, title, body);
            });
        }

        public void HandleUnreadCountChanged(TeamAccount account, int newCount)
        {
            _lastUnreadCounts.TryGetValue(account.Id, out int oldCount);
            _lastUnreadCounts[account.Id] = newCount;

            // Trigger alert only when unread count increases and account is not muted
            if (newCount > oldCount && !account.IsMuted)
            {
                // Suppress toast if user is actively viewing and focused on this chat
                if (_mainWindow.IsActive && _mainWindow.WindowState != WindowState.Minimized && _isAccountActive(account.Id))
                {
                    return;
                }

                int diff = newCount - oldCount;
                string title = $"{account.PlatformDisplayName} - {account.Name}";
                var loc = LocalizationManager.Instance;
                string body = diff == 1 
                    ? loc["OneNewMessage"] 
                    : string.Format(loc["MultipleNewMessages"], diff, newCount);

                TriggerAlert(account, title, body);
            }
        }

        private void TriggerAlert(TeamAccount account, string title, string body)
        {
            try
            {
                // 1. Play gentle notification sound
                SystemSounds.Asterisk.Play();
            }
            catch { }

            try
            {
                // 2. Flash taskbar icon if window is not active
                if (!_mainWindow.IsActive || _mainWindow.WindowState == WindowState.Minimized)
                {
                    FlashTaskbar();
                }
            }
            catch { }

            try
            {
                // 3. Show non-intrusive floating toast popup at bottom-right corner
                _currentToast?.Close();

                string badge = $"[{account.PlatformDisplayName}] {account.Name}";
                _currentToast = new NotificationToastWindow(badge, title, body, () =>
                {
                    _mainWindow.Dispatcher.Invoke(() =>
                    {
                        if (_mainWindow.WindowState == WindowState.Minimized)
                        {
                            _mainWindow.WindowState = WindowState.Normal;
                        }
                        _mainWindow.Activate();
                        _mainWindow.Focus();
                        _onAccountSelect(account);
                    });
                });

                _currentToast.Show();
            }
            catch { }
        }

        private void FlashTaskbar()
        {
            var helper = new WindowInteropHelper(_mainWindow);
            if (helper.Handle == IntPtr.Zero) return;

            var fInfo = new FLASHWINFO
            {
                cbSize = Convert.ToUInt32(Marshal.SizeOf(typeof(FLASHWINFO))),
                hwnd = helper.Handle,
                dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG,
                uCount = 4,
                dwTimeout = 0
            };

            FlashWindowEx(ref fInfo);
        }
    }
}
