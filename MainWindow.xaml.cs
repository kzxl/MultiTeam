using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using MultiTeam.Dialogs;
using MultiTeam.Models;
using MultiTeam.Services;

namespace MultiTeam
{
    public partial class MainWindow : Window
    {
        private readonly AccountStore _accountStore;
        private readonly NotificationManager _notificationManager;
        private readonly TrayIconManager _trayIcon;
        private readonly LocalizationManager _loc = LocalizationManager.Instance;

        private readonly List<TeamAccount> _allAccounts = new();
        private readonly ObservableCollection<AccountGroup> _accountGroups = new();
        private readonly Dictionary<string, WebView2> _webViews = new();
        private TeamAccount? _activeAccount;

        public bool IsSidebarCollapsed { get; set; } = false;

        private bool _isMemorySaverEnabled = false;
        private string _selectedTagColor = "#8b5cf6"; // Default: Purple

        public MainWindow()
        {
            InitializeComponent();
            _accountStore = new AccountStore();
            _notificationManager = new NotificationManager(this, async acc => await SelectAccountAsync(acc));

            _trayIcon = new TrayIconManager(
                this,
                onRestore: () =>
                {
                    Show();
                    WindowState = WindowState.Normal;
                    Activate();
                },
                onExit: () =>
                {
                    _trayIcon?.Dispose();
                    Application.Current.Shutdown();
                });

            GroupsItemsControl.ItemsSource = _accountGroups;

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyLocalization();
            LoadAccounts();

            if (_allAccounts.Count > 0)
            {
                await SelectAccountAsync(_allAccounts[0]);
            }
            else
            {
                EmptyStatePanel.Visibility = Visibility.Visible;
                TxtActiveAccountName.Text = _loc["NoAccountSelected"];
                TxtStatus.Text = string.Empty;
                BtnMuteToggle.Visibility = Visibility.Collapsed;
            }
        }

        public void ApplyLocalization()
        {
            Title = _loc["AppTitle"];
            TxtBrandSubtitle.Text = _loc["BrandSubtitle"];
            TxtAddBtnLabel.Text = _loc["BtnAddAccount"];
            TxtSearchPlaceholder.Text = _loc["SearchHint"];
            TxtNoAccountTitle.Text = _loc["NoAccountSelected"];
            TxtNoAccountGuide.Text = _loc["NoAccountGuide"];
            BtnReload.Content = _loc["Reload"];
            BtnHome.Content = _loc["Home"];
            TxtFooterLabel.Text = _loc["SessionsFolder"];
            BtnLanguageToggle.Content = _loc.LanguageToggleText;

            // Modal texts
            TxtModalTitle.Text = _loc["CreateProfileTitle"];
            TxtModalSubtitle.Text = _loc["CreateProfileSubtitle"];
            TxtSectionPlatform.Text = _loc["ChoosePlatform"];
            TxtSectionName.Text = _loc["AccountNameLabel"];
            TxtSectionGroup.Text = _loc["SectionGroup"];
            TxtSectionColor.Text = _loc["ColorTagLabel"];
            TxtModalCustomUrlLabel.Text = _loc["CustomUrlHint"];
            BtnModalCancel.Content = _loc["Cancel"];
            BtnModalSubmit.Content = _loc["Create"];

            UpdateMemorySaverButtonText();
            UpdateMuteButtonState();

            if (_activeAccount != null)
            {
                TxtActiveAccountName.Text = $"{_activeAccount.Name} ({_activeAccount.PlatformDisplayName})";
            }
        }

        private void BtnLanguageToggle_Click(object sender, RoutedEventArgs e)
        {
            _loc.ToggleLanguage();
            ApplyLocalization();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            if (WindowState == WindowState.Minimized)
            {
                Hide(); // Minimize to system tray
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _trayIcon?.Dispose();
        }

        private void LoadAccounts()
        {
            _allAccounts.Clear();
            var list = _accountStore.LoadAccounts();
            _allAccounts.AddRange(list);
            RefreshDisplayedAccounts();
        }

        private void RefreshDisplayedAccounts()
        {
            string filter = TxtSearchAccounts.Text.Trim();

            var query = _allAccounts.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(filter))
            {
                query = query.Where(a => a.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                         a.GroupName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                         a.PlatformDisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase));
            }

            // Group accounts by GroupName
            var groupedDict = query
                .GroupBy(a => string.IsNullOrWhiteSpace(a.GroupName) ? _loc["DefaultGroup"] : a.GroupName.Trim())
                .OrderBy(g => g.Key)
                .ToList();

            _accountGroups.Clear();

            foreach (var g in groupedDict)
            {
                var groupVm = new AccountGroup
                {
                    Name = g.Key,
                    IsExpanded = true
                };

                // Order accounts within group by Pinned first, then CreatedAt
                var accountsInGroup = g.OrderByDescending(a => a.IsPinned).ThenBy(a => a.CreatedAt);
                foreach (var acc in accountsInGroup)
                {
                    groupVm.Accounts.Add(acc);
                }

                groupVm.NotifyStateChanged();
                _accountGroups.Add(groupVm);
            }
        }

        private void GroupHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.DataContext is AccountGroup group)
            {
                group.IsExpanded = !group.IsExpanded;
            }
        }

        private void GroupSuggestion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string groupName)
            {
                TxtModalGroupName.Text = groupName;
            }
        }

        private async Task SelectAccountAsync(TeamAccount account)
        {
            _activeAccount = account;

            foreach (var acc in _allAccounts)
            {
                acc.IsActive = (acc.Id == account.Id);
            }

            TxtActiveAccountName.Text = $"{account.Name} ({account.PlatformDisplayName})";
            EmptyStatePanel.Visibility = Visibility.Collapsed;
            BtnMuteToggle.Visibility = Visibility.Visible;
            UpdateMuteButtonState();

            await EnsureWebViewForAccountAsync(account);

            foreach (var kvp in _webViews)
            {
                bool isTarget = (kvp.Key == account.Id);
                kvp.Value.Visibility = isTarget ? Visibility.Visible : Visibility.Collapsed;

                if (_isMemorySaverEnabled)
                {
                    if (isTarget)
                    {
                        kvp.Value.CoreWebView2?.Resume();
                    }
                    else
                    {
                        _ = kvp.Value.CoreWebView2?.TrySuspendAsync();
                    }
                }
            }

            if (_webViews.TryGetValue(account.Id, out var activeWebView))
            {
                TxtZoomLevel.Text = $"{(int)(activeWebView.ZoomFactor * 100)}%";
            }
        }

        private void UpdateMuteButtonState()
        {
            if (_activeAccount == null) return;
            BtnMuteToggle.Content = _activeAccount.IsMuted ? _loc["MuteOn"] : _loc["MuteOff"];
        }

        private void UpdateMemorySaverButtonText()
        {
            BtnMemorySaver.Content = _isMemorySaverEnabled ? _loc["MemorySaverOn"] : _loc["MemorySaverOff"];
            BtnMemorySaver.Foreground = _isMemorySaverEnabled 
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129)) 
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(203, 213, 225));
        }

        private async Task EnsureWebViewForAccountAsync(TeamAccount account)
        {
            if (_webViews.ContainsKey(account.Id))
            {
                return;
            }

            TxtStatus.Text = _loc["Initializing"];

            var webView = new WebView2
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Visibility = Visibility.Collapsed
            };

            WebViewsContainer.Children.Add(webView);
            _webViews[account.Id] = webView;

            try
            {
                string profileFolder = _accountStore.GetProfileFolder(account.Id);
                var env = await CoreWebView2Environment.CreateAsync(userDataFolder: profileFolder);
                await webView.EnsureCoreWebView2Async(env);

                // Auto-allow notifications, mic and camera
                webView.CoreWebView2.PermissionRequested += (s, args) =>
                {
                    if (args.PermissionKind == CoreWebView2PermissionKind.Notifications ||
                        args.PermissionKind == CoreWebView2PermissionKind.Microphone ||
                        args.PermissionKind == CoreWebView2PermissionKind.Camera)
                    {
                        args.State = CoreWebView2PermissionState.Allow;
                    }
                };

                // Native Notification listener
                try
                {
                    webView.CoreWebView2.NotificationReceived += (s, args) =>
                    {
                        string title = args.Notification.Title ?? string.Empty;
                        string body = args.Notification.Body ?? string.Empty;

                        _notificationManager.HandleNotificationReceived(account, title, body);
                    };
                }
                catch { }

                // Title tracking for unread badge count
                webView.CoreWebView2.DocumentTitleChanged += (s, args) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        UpdateUnreadCountFromTitle(account, webView.CoreWebView2.DocumentTitle);
                    });
                };

                // Status tracking
                webView.NavigationStarting += (s, args) =>
                {
                    if (_activeAccount?.Id == account.Id)
                    {
                        TxtStatus.Text = _loc["Loading"];
                    }
                };

                webView.NavigationCompleted += (s, args) =>
                {
                    if (_activeAccount?.Id == account.Id)
                    {
                        TxtStatus.Text = args.IsSuccess ? _loc["Ready"] : _loc["ConnectionError"];
                    }
                };

                // Handle external links safely
                webView.CoreWebView2.NewWindowRequested += (s, args) =>
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

                    // If custom platform, allow custom domain
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
                };

                webView.Source = new Uri(account.DefaultUrl);
            }
            catch (Exception ex)
            {
                TxtStatus.Text = _loc["ConnectionError"];
                MessageBox.Show($"Failed to initialize WebView2 for '{account.Name}':\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateUnreadCountFromTitle(TeamAccount account, string title)
        {
            int newCount = 0;
            if (!string.IsNullOrEmpty(title))
            {
                var match = Regex.Match(title, @"\((\d+)\)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int count))
                {
                    newCount = count;
                }
            }

            account.UnreadCount = newCount;
            _notificationManager.HandleUnreadCountChanged(account, newCount);

            // Update parent group state
            foreach (var group in _accountGroups)
            {
                if (group.Accounts.Contains(account))
                {
                    group.NotifyStateChanged();
                }
            }
        }

        private async void AccountItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is TeamAccount account)
            {
                await SelectAccountAsync(account);
            }
        }

        #region Sidebar & Search Controls

        private void BtnToggleSidebar_Click(object sender, RoutedEventArgs e)
        {
            IsSidebarCollapsed = !IsSidebarCollapsed;

            if (IsSidebarCollapsed)
            {
                SidebarColumn.Width = new GridLength(68);
                BrandTextPanel.Visibility = Visibility.Collapsed;
                TxtAddBtnLabel.Visibility = Visibility.Collapsed;
                SearchPanel.Visibility = Visibility.Collapsed;
                TxtFooterLabel.Visibility = Visibility.Collapsed;
            }
            else
            {
                SidebarColumn.Width = new GridLength(300);
                BrandTextPanel.Visibility = Visibility.Visible;
                TxtAddBtnLabel.Visibility = Visibility.Visible;
                SearchPanel.Visibility = Visibility.Visible;
                TxtFooterLabel.Visibility = Visibility.Visible;
            }

            RefreshDisplayedAccounts();
        }

        private void TxtSearchAccounts_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshDisplayedAccounts();
        }

        #endregion

        #region Modern In-App Modal: Create Chat Profile & Color Tagging

        private void BtnOpenCreateModal_Click(object sender, RoutedEventArgs e)
        {
            // Hide WebViews to prevent HWND airspace and focus stealing
            WebViewsContainer.Visibility = Visibility.Collapsed;
            CreateProfileOverlay.Visibility = Visibility.Visible;

            TxtModalAccountName.Text = string.Empty;
            TxtModalCustomUrl.Text = string.Empty;
            TxtModalGroupName.Text = _loc["DefaultGroup"];
            ModalCustomUrlPanel.Visibility = Visibility.Collapsed;

            OptTeams.IsChecked = true;
            SelectColorSwatch(SwatchPurple, "#8b5cf6");

            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
            {
                TxtModalAccountName.Focus();
                Keyboard.Focus(TxtModalAccountName);
            }));
        }

        private void BtnCloseModal_Click(object sender, RoutedEventArgs e)
        {
            CreateProfileOverlay.Visibility = Visibility.Collapsed;
            WebViewsContainer.Visibility = Visibility.Visible;
        }

        private void CreateProfileOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            BtnCloseModal_Click(sender, e);
        }

        private void ModalCard_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void PlatformChip_Click(object sender, RoutedEventArgs e)
        {
            ModalCustomUrlPanel.Visibility = (OptCustom.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;

            if (OptTeams.IsChecked == true) SelectColorSwatch(SwatchPurple, "#4f46e5");
            else if (OptZalo.IsChecked == true) SelectColorSwatch(SwatchBlue, "#0068ff");
            else if (OptTelegram.IsChecked == true) SelectColorSwatch(SwatchBlue, "#0284c7");
            else if (OptMessenger.IsChecked == true) SelectColorSwatch(SwatchBlue, "#0084ff");
            else if (OptWhatsApp.IsChecked == true) SelectColorSwatch(SwatchGreen, "#25d366");
            else if (OptDiscord.IsChecked == true) SelectColorSwatch(SwatchPurple, "#5865f2");
            else if (OptSlack.IsChecked == true) SelectColorSwatch(SwatchPink, "#e01e5a");
            else if (OptSkype.IsChecked == true) SelectColorSwatch(SwatchBlue, "#00aff0");
            else if (OptGoogleChat.IsChecked == true) SelectColorSwatch(SwatchGreen, "#00ac47");
        }

        private void Swatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string color)
            {
                SelectColorSwatch(border, color);
            }
        }

        private void SelectColorSwatch(Border selectedBorder, string color)
        {
            _selectedTagColor = color;

            var swatches = new[] { SwatchRed, SwatchOrange, SwatchYellow, SwatchGreen, SwatchBlue, SwatchPurple, SwatchPink, SwatchSlate };
            foreach (var b in swatches)
            {
                b.BorderThickness = (b == selectedBorder) ? new Thickness(2.5) : new Thickness(0);
            }
        }

        private async void BtnModalSubmit_Click(object sender, RoutedEventArgs e)
        {
            await SubmitCreateProfileAsync();
        }

        private async void TxtModalAccountName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await SubmitCreateProfileAsync();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                BtnCloseModal_Click(sender, e);
                e.Handled = true;
            }
        }

        private async Task SubmitCreateProfileAsync()
        {
            string name = TxtModalAccountName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(_loc["EnterAccountNameWarning"], "Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtModalAccountName.Focus();
                return;
            }

            PlatformType platform = PlatformType.Teams;
            if (OptZalo.IsChecked == true) platform = PlatformType.Zalo;
            else if (OptTelegram.IsChecked == true) platform = PlatformType.Telegram;
            else if (OptMessenger.IsChecked == true) platform = PlatformType.Messenger;
            else if (OptWhatsApp.IsChecked == true) platform = PlatformType.WhatsApp;
            else if (OptDiscord.IsChecked == true) platform = PlatformType.Discord;
            else if (OptSlack.IsChecked == true) platform = PlatformType.Slack;
            else if (OptSkype.IsChecked == true) platform = PlatformType.Skype;
            else if (OptGoogleChat.IsChecked == true) platform = PlatformType.GoogleChat;
            else if (OptCustom.IsChecked == true) platform = PlatformType.Custom;

            string customUrl = TxtModalCustomUrl.Text.Trim();
            if (platform == PlatformType.Custom)
            {
                if (string.IsNullOrWhiteSpace(customUrl) || (!customUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !customUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show(_loc["EnterCustomUrlWarning"], "Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
                    TxtModalCustomUrl.Focus();
                    return;
                }
            }

            string groupName = TxtModalGroupName.Text.Trim();
            if (string.IsNullOrWhiteSpace(groupName))
            {
                groupName = _loc["DefaultGroup"];
            }

            CreateProfileOverlay.Visibility = Visibility.Collapsed;
            WebViewsContainer.Visibility = Visibility.Visible;

            string accountId = "acc_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var newAccount = new TeamAccount
            {
                Id = accountId,
                Name = name,
                GroupName = groupName,
                Platform = platform,
                CustomUrl = customUrl,
                TagColor = _selectedTagColor,
                CreatedAt = DateTime.Now
            };

            _allAccounts.Add(newAccount);
            _accountStore.SaveAccounts(_allAccounts);
            RefreshDisplayedAccounts();

            await SelectAccountAsync(newAccount);
        }

        #endregion

        #region Zoom, Memory Saver & Navigation

        private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            if (_activeAccount != null && _webViews.TryGetValue(_activeAccount.Id, out var wv))
            {
                wv.ZoomFactor = Math.Min(2.0, Math.Round(wv.ZoomFactor + 0.1, 1));
                TxtZoomLevel.Text = $"{(int)(wv.ZoomFactor * 100)}%";
            }
        }

        private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            if (_activeAccount != null && _webViews.TryGetValue(_activeAccount.Id, out var wv))
            {
                wv.ZoomFactor = Math.Max(0.6, Math.Round(wv.ZoomFactor - 0.1, 1));
                TxtZoomLevel.Text = $"{(int)(wv.ZoomFactor * 100)}%";
            }
        }

        private void TxtZoomLevel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_activeAccount != null && _webViews.TryGetValue(_activeAccount.Id, out var wv))
            {
                wv.ZoomFactor = 1.0;
                TxtZoomLevel.Text = "100%";
            }
        }

        private void BtnMemorySaver_Click(object sender, RoutedEventArgs e)
        {
            _isMemorySaverEnabled = !_isMemorySaverEnabled;
            UpdateMemorySaverButtonText();

            if (_isMemorySaverEnabled && _activeAccount != null)
            {
                foreach (var kvp in _webViews)
                {
                    if (kvp.Key != _activeAccount.Id)
                    {
                        _ = kvp.Value.CoreWebView2?.TrySuspendAsync();
                    }
                }
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (_activeAccount != null && _webViews.TryGetValue(_activeAccount.Id, out var webView))
            {
                if (webView.CanGoBack) webView.GoBack();
            }
        }

        private void BtnForward_Click(object sender, RoutedEventArgs e)
        {
            if (_activeAccount != null && _webViews.TryGetValue(_activeAccount.Id, out var webView))
            {
                if (webView.CanGoForward) webView.GoForward();
            }
        }

        private void BtnMuteToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_activeAccount != null)
            {
                _activeAccount.IsMuted = !_activeAccount.IsMuted;
                _accountStore.SaveAccounts(_allAccounts);
                UpdateMuteButtonState();
            }
        }

        private void BtnReload_Click(object sender, RoutedEventArgs e)
        {
            if (_activeAccount != null && _webViews.TryGetValue(_activeAccount.Id, out var webView))
            {
                webView.Reload();
            }
        }

        private void BtnHome_Click(object sender, RoutedEventArgs e)
        {
            if (_activeAccount != null && _webViews.TryGetValue(_activeAccount.Id, out var webView))
            {
                webView.CoreWebView2?.Navigate(_activeAccount.DefaultUrl);
            }
        }

        private void BtnOpenSessionsFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("explorer.exe", _accountStore.SessionsDirectory);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Cannot open folder: {ex.Message}", "Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        #endregion

        #region Keyboard Shortcuts

        private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Key >= Key.D1 && e.Key <= Key.D9)
                {
                    int index = e.Key - Key.D1;
                    if (index < _allAccounts.Count)
                    {
                        await SelectAccountAsync(_allAccounts[index]);
                        e.Handled = true;
                    }
                }
                else if (e.Key == Key.F)
                {
                    if (IsSidebarCollapsed)
                    {
                        BtnToggleSidebar_Click(sender, e);
                    }
                    TxtSearchAccounts.Focus();
                    TxtSearchAccounts.SelectAll();
                    e.Handled = true;
                }
                else if (e.Key == Key.OemPlus || e.Key == Key.Add)
                {
                    BtnZoomIn_Click(sender, e);
                    e.Handled = true;
                }
                else if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
                {
                    BtnZoomOut_Click(sender, e);
                    e.Handled = true;
                }
                else if (e.Key == Key.D0 || e.Key == Key.NumPad0)
                {
                    TxtZoomLevel_MouseLeftButtonDown(sender, null!);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Escape && CreateProfileOverlay.Visibility == Visibility.Visible)
            {
                BtnCloseModal_Click(sender, e);
                e.Handled = true;
            }
        }

        #endregion

        #region Context Menu Actions

        private void MenuItemTogglePin_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is TeamAccount account)
            {
                account.IsPinned = !account.IsPinned;
                _accountStore.SaveAccounts(_allAccounts);
                RefreshDisplayedAccounts();
            }
        }

        private void MenuItemToggleMute_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is TeamAccount account)
            {
                account.IsMuted = !account.IsMuted;
                _accountStore.SaveAccounts(_allAccounts);
                if (_activeAccount?.Id == account.Id)
                {
                    UpdateMuteButtonState();
                }
            }
        }

        private void MenuItemSetColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is TeamAccount account)
            {
                string color = menuItem.Tag?.ToString() ?? string.Empty;
                account.TagColor = color;
                _accountStore.SaveAccounts(_allAccounts);
                RefreshDisplayedAccounts();
            }
        }

        private void MenuItemMoveGroup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is TeamAccount account)
            {
                var dialog = new AddAccountDialog
                {
                    Owner = this,
                    Title = _loc["MoveToGroup"]
                };
                dialog.TxtHeaderTitle.Text = _loc["MoveToGroup"];
                dialog.TxtSubtitle.Text = "Nhập tên nhóm mới để chuyển tài khoản này vào:";
                dialog.BtnConfirm.Content = "Chuyển nhóm";
                dialog.TxtAccountName.Text = account.GroupName;

                if (dialog.ShowDialog() == true)
                {
                    string newGroup = dialog.AccountName.Trim();
                    if (!string.IsNullOrWhiteSpace(newGroup))
                    {
                        account.GroupName = newGroup;
                        _accountStore.SaveAccounts(_allAccounts);
                        RefreshDisplayedAccounts();
                    }
                }
            }
        }

        private async void MenuItemClearCache_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is TeamAccount account)
            {
                string msg = string.Format(_loc["ConfirmClearCacheMsg"], account.Name, account.PlatformDisplayName);
                var result = MessageBox.Show(
                    msg,
                    _loc["ConfirmClearCacheTitle"],
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    if (_webViews.TryGetValue(account.Id, out var webView) && webView.CoreWebView2 != null)
                    {
                        await webView.CoreWebView2.Profile.ClearBrowsingDataAsync();
                        webView.CoreWebView2.Navigate(account.DefaultUrl);
                        MessageBox.Show(string.Format(_loc["ClearCacheSuccess"], account.Name), "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
        }

        private void MenuItemRename_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is TeamAccount account)
            {
                Keyboard.ClearFocus();

                var dialog = new AddAccountDialog
                {
                    Owner = this,
                    Title = _loc["RenameTitle"]
                };
                dialog.TxtHeaderTitle.Text = _loc["RenameTitle"];
                dialog.TxtSubtitle.Text = _loc["RenameSubtitle"];
                dialog.BtnConfirm.Content = _loc["SaveChange"];
                dialog.TxtAccountName.Text = account.Name;

                if (dialog.ShowDialog() == true)
                {
                    account.Name = dialog.AccountName;
                    _accountStore.SaveAccounts(_allAccounts);
                    RefreshDisplayedAccounts();
                    if (_activeAccount?.Id == account.Id)
                    {
                        TxtActiveAccountName.Text = $"{account.Name} ({account.PlatformDisplayName})";
                    }
                }
            }
        }

        private void MenuItemOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is TeamAccount account)
            {
                string folder = _accountStore.GetProfileFolder(account.Id);
                try
                {
                    Process.Start("explorer.exe", folder);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Cannot open folder: {ex.Message}", "Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private async void MenuItemDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is TeamAccount account)
            {
                string msg = string.Format(_loc["ConfirmDeleteMsg"], account.Name, account.PlatformDisplayName);
                var result = MessageBox.Show(
                    msg,
                    _loc["ConfirmDeleteTitle"],
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    if (_webViews.TryGetValue(account.Id, out var webView))
                    {
                        WebViewsContainer.Children.Remove(webView);
                        webView.Dispose();
                        _webViews.Remove(account.Id);
                    }

                    _allAccounts.Remove(account);
                    _accountStore.SaveAccounts(_allAccounts);
                    RefreshDisplayedAccounts();

                    if (_activeAccount?.Id == account.Id)
                    {
                        if (_allAccounts.Count > 0)
                        {
                            await SelectAccountAsync(_allAccounts[0]);
                        }
                        else
                        {
                            _activeAccount = null;
                            EmptyStatePanel.Visibility = Visibility.Visible;
                            TxtActiveAccountName.Text = _loc["NoAccountSelected"];
                            TxtStatus.Text = string.Empty;
                            BtnMuteToggle.Visibility = Visibility.Collapsed;
                        }
                    }
                }
            }
        }

        #endregion
    }
}
