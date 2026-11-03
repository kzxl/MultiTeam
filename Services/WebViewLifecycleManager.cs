using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using MultiTeam.Models;

namespace MultiTeam.Services
{
    public class WebViewLifecycleManager : IDisposable
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessWorkingSetSize(IntPtr proc, IntPtr min, IntPtr max);

        private readonly Panel _container;
        private readonly AccountStore _accountStore;
        private readonly Dictionary<string, WebView2> _webViews = new();
        private readonly Dictionary<string, DateTime> _lastActiveTimes = new();
        private readonly Dictionary<string, TeamAccount> _trackedAccounts = new();
        private readonly DispatcherTimer _inactivityTimer;

        public bool IsAutoSleepEnabled { get; set; } = true;
        public TimeSpan InactivitySuspendThreshold { get; set; } = TimeSpan.FromMinutes(10);
        public TimeSpan InactivityHibernateThreshold { get; set; } = TimeSpan.FromMinutes(30);

        public event Action<string, string, string>? NotificationReceived;
        public event Action<string, string>? TitleChanged;
        public event Action<string, bool>? NavigationStateChanged;
        public event Action<string, Exception?>? InitializationError;
        public event Action<long>? MemoryUsageUpdated;

        private string? _currentActiveAccountId;

        public WebViewLifecycleManager(Panel container, AccountStore accountStore)
        {
            _container = container;
            _accountStore = accountStore;

            _inactivityTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _inactivityTimer.Tick += InactivityTimer_Tick;
            _inactivityTimer.Start();
        }

        public WebView2? GetWebView(string accountId)
        {
            _webViews.TryGetValue(accountId, out var wv);
            return wv;
        }

        public async Task<WebView2?> ActivateAccountAsync(TeamAccount account)
        {
            _currentActiveAccountId = account.Id;
            _lastActiveTimes[account.Id] = DateTime.Now;
            _trackedAccounts[account.Id] = account;

            // 1. Ensure WebView instance exists
            var webView = await EnsureWebViewInstanceAsync(account);
            if (webView == null)
            {
                return null;
            }

            // 2. Resume if suspended
            if (account.LifecycleState == AccountLifecycleState.Suspended)
            {
                try
                {
                    webView.CoreWebView2?.Resume();
                }
                catch { }
            }

            account.LifecycleState = AccountLifecycleState.Active;

            // 3. Update visibility
            foreach (var kvp in _webViews)
            {
                bool isTarget = (kvp.Key == account.Id);
                kvp.Value.Visibility = isTarget ? Visibility.Visible : Visibility.Collapsed;
            }

            return webView;
        }

        private async Task<WebView2?> EnsureWebViewInstanceAsync(TeamAccount account)
        {
            if (_webViews.TryGetValue(account.Id, out var existing))
            {
                return existing;
            }

            var webView = new WebView2
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Visibility = Visibility.Collapsed
            };

            _container.Children.Add(webView);
            _webViews[account.Id] = webView;

            try
            {
                string profileFolder = _accountStore.GetProfileFolder(account.Id);
                var env = await CoreWebView2Environment.CreateAsync(userDataFolder: profileFolder);
                await webView.EnsureCoreWebView2Async(env);

                string accountId = account.Id;

                // Auto-allow notifications, microphone, camera
                webView.CoreWebView2.PermissionRequested += (s, args) =>
                {
                    if (args.PermissionKind == CoreWebView2PermissionKind.Notifications ||
                        args.PermissionKind == CoreWebView2PermissionKind.Microphone ||
                        args.PermissionKind == CoreWebView2PermissionKind.Camera)
                    {
                        args.State = CoreWebView2PermissionState.Allow;
                    }
                };

                // Native notification listener
                try
                {
                    webView.CoreWebView2.NotificationReceived += (s, args) =>
                    {
                        _lastActiveTimes[accountId] = DateTime.Now;
                        string title = args.Notification.Title ?? string.Empty;
                        string body = args.Notification.Body ?? string.Empty;
                        NotificationReceived?.Invoke(accountId, title, body);
                    };
                }
                catch { }

                // Title tracking
                webView.CoreWebView2.DocumentTitleChanged += (s, args) =>
                {
                    _lastActiveTimes[accountId] = DateTime.Now;
                    TitleChanged?.Invoke(accountId, webView.CoreWebView2.DocumentTitle ?? string.Empty);
                };

                // Navigation status
                webView.NavigationStarting += (s, args) =>
                {
                    NavigationStateChanged?.Invoke(accountId, false);
                };

                webView.NavigationCompleted += (s, args) =>
                {
                    NavigationStateChanged?.Invoke(accountId, args.IsSuccess);
                };

                // External link handler
                webView.CoreWebView2.NewWindowRequested += (s, args) =>
                {
                    HandleExternalLink(account, args);
                };

                webView.Source = new Uri(account.DefaultUrl);
                account.LifecycleState = AccountLifecycleState.Ready;
                return webView;
            }
            catch (Exception ex)
            {
                _container.Children.Remove(webView);
                _webViews.Remove(account.Id);
                InitializationError?.Invoke(account.Name, ex);
                return null;
            }
        }

        private void HandleExternalLink(TeamAccount account, CoreWebView2NewWindowRequestedEventArgs args)
        {
            string uri = args.Uri ?? string.Empty;
            bool isInternal = uri.Contains("teams.microsoft.com", StringComparison.OrdinalIgnoreCase) ||
                             uri.Contains("zalo.me", StringComparison.OrdinalIgnoreCase) ||
                             uri.Contains("telegram.org", StringComparison.OrdinalIgnoreCase) ||
                             uri.Contains("facebook.com", StringComparison.OrdinalIgnoreCase) ||
                             uri.Contains("messenger.com", StringComparison.OrdinalIgnoreCase) ||
                             uri.Contains("whatsapp.com", StringComparison.OrdinalIgnoreCase) ||
                             uri.Contains("discord.com", StringComparison.OrdinalIgnoreCase) ||
                             uri.Contains("slack.com", StringComparison.OrdinalIgnoreCase) ||
                             uri.Contains("skype.com", StringComparison.OrdinalIgnoreCase) ||
                             uri.Contains("google.com", StringComparison.OrdinalIgnoreCase) ||
                             uri.Contains("live.com", StringComparison.OrdinalIgnoreCase) ||
                             uri.Contains("microsoftonline.com", StringComparison.OrdinalIgnoreCase);

            if (!isInternal && account.Platform == PlatformType.Custom && !string.IsNullOrWhiteSpace(account.CustomUrl))
            {
                try
                {
                    var customHost = new Uri(account.CustomUrl).Host;
                    if (uri.Contains(customHost, StringComparison.OrdinalIgnoreCase))
                    {
                        isInternal = true;
                    }
                }
                catch { }
            }

            if (!isInternal)
            {
                args.Handled = true;
                try
                {
                    Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
                }
                catch { }
            }
        }

        public async Task SuspendAccountAsync(TeamAccount account)
        {
            if (account.Id == _currentActiveAccountId) return; // Do not suspend currently viewed account

            if (_webViews.TryGetValue(account.Id, out var webView) && webView.CoreWebView2 != null)
            {
                try
                {
                    await webView.CoreWebView2.TrySuspendAsync();
                    account.LifecycleState = AccountLifecycleState.Suspended;
                }
                catch { }
            }
        }

        public void HibernateAccount(TeamAccount account)
        {
            if (account.Id == _currentActiveAccountId) return; // Do not hibernate currently active account

            if (_webViews.TryGetValue(account.Id, out var webView))
            {
                try
                {
                    _container.Children.Remove(webView);
                    webView.Dispose();
                }
                catch { }
                _webViews.Remove(account.Id);
                account.LifecycleState = AccountLifecycleState.Hibernated;
            }
        }

        public async Task SuspendAllBackgroundAsync(List<TeamAccount> allAccounts)
        {
            foreach (var acc in allAccounts)
            {
                if (acc.Id != _currentActiveAccountId)
                {
                    await SuspendAccountAsync(acc);
                }
            }
        }

        public long TrimMemory()
        {
            long beforeBytes = GetTotalMemoryUsageBytes();

            try
            {
                // 1. Force full garbage collection
                GC.Collect(2, GCCollectionMode.Forced, true, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced, true, true);

                // 2. Win32 EmptyWorkingSet
                SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, (IntPtr)(-1), (IntPtr)(-1));
            }
            catch { }

            long afterBytes = GetTotalMemoryUsageBytes();
            long freed = Math.Max(0, beforeBytes - afterBytes);

            MemoryUsageUpdated?.Invoke(afterBytes);
            return freed;
        }

        public long GetTotalMemoryUsageBytes()
        {
            long total = 0;
            try
            {
                total += Process.GetCurrentProcess().WorkingSet64;

                var webviewProcesses = Process.GetProcessesByName("msedgewebview2");
                foreach (var p in webviewProcesses)
                {
                    try
                    {
                        total += p.WorkingSet64;
                    }
                    catch { }
                }
            }
            catch { }
            return total;
        }

        public void DisposeAccount(string accountId)
        {
            if (_webViews.TryGetValue(accountId, out var webView))
            {
                try
                {
                    _container.Children.Remove(webView);
                    webView.Dispose();
                }
                catch { }
                _webViews.Remove(accountId);
            }
            _lastActiveTimes.Remove(accountId);
            _trackedAccounts.Remove(accountId);
            if (_currentActiveAccountId == accountId)
            {
                _currentActiveAccountId = null;
            }
        }

        private async void InactivityTimer_Tick(object? sender, EventArgs e)
        {
            if (!IsAutoSleepEnabled) return;

            DateTime now = DateTime.Now;
            var accountsToCheck = _webViews.Keys.ToList();

            foreach (var accId in accountsToCheck)
            {
                if (accId == _currentActiveAccountId) continue;

                if (_lastActiveTimes.TryGetValue(accId, out var lastActive))
                {
                    var idleTime = now - lastActive;

                    if (idleTime > InactivityHibernateThreshold)
                    {
                        // Deep hibernate (30m+ idle)
                        if (_webViews.TryGetValue(accId, out var wv))
                        {
                            try
                            {
                                _container.Children.Remove(wv);
                                wv.Dispose();
                            }
                            catch { }
                            _webViews.Remove(accId);

                            if (_trackedAccounts.TryGetValue(accId, out var acc))
                            {
                                acc.LifecycleState = AccountLifecycleState.Hibernated;
                            }
                        }
                    }
                    else if (idleTime > InactivitySuspendThreshold)
                    {
                        // Light suspend (10m+ idle)
                        if (_webViews.TryGetValue(accId, out var wv) && wv.CoreWebView2 != null)
                        {
                            try
                            {
                                await wv.CoreWebView2.TrySuspendAsync();
                                if (_trackedAccounts.TryGetValue(accId, out var acc))
                                {
                                    acc.LifecycleState = AccountLifecycleState.Suspended;
                                }
                            }
                            catch { }
                        }
                    }
                }
            }

            MemoryUsageUpdated?.Invoke(GetTotalMemoryUsageBytes());
        }

        public void Dispose()
        {
            _inactivityTimer.Stop();

            foreach (var kvp in _webViews)
            {
                try
                {
                    _container.Children.Remove(kvp.Value);
                    kvp.Value.Dispose();
                }
                catch { }
            }
            _webViews.Clear();
            _lastActiveTimes.Clear();
        }
    }
}
