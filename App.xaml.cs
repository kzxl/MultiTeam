using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;

namespace MultiTeam;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    protected override void OnStartup(StartupEventArgs e)
    {
        Process current = Process.GetCurrentProcess();
        var existingProcesses = Process.GetProcessesByName(current.ProcessName)
                                       .Where(p => p.Id != current.Id)
                                       .ToList();

        if (existingProcesses.Count > 0)
        {
            // Another instance is legitimately running, bring it to front
            foreach (var process in existingProcesses)
            {
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    ShowWindow(process.MainWindowHandle, SW_RESTORE);
                    SetForegroundWindow(process.MainWindowHandle);
                    break;
                }
            }

            Shutdown();
            return;
        }

        base.OnStartup(e);
    }
}
