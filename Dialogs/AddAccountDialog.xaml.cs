using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace MultiTeam.Dialogs
{
    public partial class AddAccountDialog : Window
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr hWnd);

        public string AccountName { get; private set; } = string.Empty;

        public AddAccountDialog()
        {
            InitializeComponent();

            Loaded += AddAccountDialog_Loaded;
            ContentRendered += AddAccountDialog_ContentRendered;
        }

        private void AddAccountDialog_Loaded(object sender, RoutedEventArgs e)
        {
            ForceKeyboardFocus();
        }

        private void AddAccountDialog_ContentRendered(object? sender, EventArgs e)
        {
            ForceKeyboardFocus();
        }

        private void ForceKeyboardFocus()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                var helper = new WindowInteropHelper(this);
                if (helper.Handle != IntPtr.Zero)
                {
                    SetForegroundWindow(helper.Handle);
                    SetFocus(helper.Handle);
                }

                TxtAccountName.Focus();
                Keyboard.Focus(TxtAccountName);
                TxtAccountName.SelectAll();
            }));
        }

        private void TxtAccountName_GotFocus(object sender, RoutedEventArgs e)
        {
            TxtAccountName.SelectAll();
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            Submit();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TxtAccountName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Submit();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
                e.Handled = true;
            }
        }

        private void Submit()
        {
            string name = TxtAccountName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Vui lòng nhập tên tài khoản.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtAccountName.Focus();
                return;
            }

            AccountName = name;
            DialogResult = true;
            Close();
        }
    }
}
