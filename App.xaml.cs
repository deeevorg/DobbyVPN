using System.Windows;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace RismanNG;
public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            Infrastructure.StartupTrace.Write("OnStartup entered");
            base.OnStartup(e);
            var window = new MainWindow { Topmost = true, WindowState = WindowState.Normal };
            Infrastructure.StartupTrace.Write("MainWindow constructed");
            MainWindow = window;
            window.Show();
            window.Activate();
            var handle = new WindowInteropHelper(window).Handle;
            ShowWindow(handle, 9);
            SetForegroundWindow(handle);
            GetWindowRect(handle, out var rect);
            Infrastructure.StartupTrace.Write($"MainWindow shown visible={IsWindowVisible(handle)} rect={rect.Left},{rect.Top},{rect.Right},{rect.Bottom}");
        }
        catch (Exception exception)
        {
            Infrastructure.StartupTrace.Write(exception);
            MessageBox.Show("RismanNG could not start. See the startup log.", "RismanNG", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint hWnd, int command);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hWnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hWnd, out Rectangle rectangle);
    private struct Rectangle { public int Left; public int Top; public int Right; public int Bottom; }
}
