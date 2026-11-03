using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace MultiTeam;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool SetDllDirectory(string lpPathName);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    public static readonly uint WM_SHOW_MULTITEAM = RegisterWindowMessage("MultiTeam_Restore_Window_Instance");
    public static readonly IntPtr HWND_BROADCAST = (IntPtr)0xFFFF;

    private static Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Global unhandled exception handlers for user diagnostics
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            MessageBox.Show($"Application Error (Fatal):\n{args.ExceptionObject}", "MultiTeam Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        DispatcherUnhandledException += (s, args) =>
        {
            MessageBox.Show($"Dispatcher Error:\n{args.Exception?.Message}\n{args.Exception?.StackTrace}", "MultiTeam Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        // Ensure native DLLs (e.g. WebView2Loader.dll) are located in base directory
        try
        {
            SetDllDirectory(AppDomain.CurrentDomain.BaseDirectory);
        }
        catch { }

        // Robust single-instance lock via Mutex + Win32 Broadcast wakeup
        bool createdNew = true;
        try
        {
            _mutex = new Mutex(true, "MultiTeam_SingleInstance_Mutex_kzxl", out createdNew);
        }
        catch
        {
            createdNew = true;
        }

        if (!createdNew)
        {
            // Another instance is legitimately running - broadcast message to bring it to front
            PostMessage(HWND_BROADCAST, WM_SHOW_MULTITEAM, IntPtr.Zero, IntPtr.Zero);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        try
        {
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi khởi động giao diện:\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}",
                "MultiTeam Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _mutex?.ReleaseMutex();
        }
        catch { }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
