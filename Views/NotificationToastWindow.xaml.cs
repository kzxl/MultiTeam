using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace MultiTeam.Views
{
    public partial class NotificationToastWindow : Window
    {
        private readonly DispatcherTimer _timer;
        private readonly Action? _onClick;

        public NotificationToastWindow(string accountName, string title, string body, Action? onClick = null)
        {
            InitializeComponent();
            _onClick = onClick;

            TxtAccountBadge.Text = accountName;
            TxtTitle.Text = string.IsNullOrWhiteSpace(title) ? "Tin nhắn mới" : title;
            TxtBody.Text = string.IsNullOrWhiteSpace(body) ? "Bạn vừa nhận được một tin nhắn mới." : body;

            // Position at bottom-right corner
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 20;
            Top = workArea.Bottom - Height - 20;

            // Auto-close timer (5 seconds)
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _timer.Tick += (s, e) =>
            {
                _timer.Stop();
                Close();
            };
            _timer.Start();
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _timer.Stop();
            _onClick?.Invoke();
            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            Close();
        }
    }
}
