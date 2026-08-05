using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TELLLauncher.Views;

/// <summary>
/// 窗口辅助工具：统一应用暗色标题栏。
/// </summary>
public static class WindowHelper
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    /// <summary>
    /// 将指定窗口的标题栏设为暗色。
    /// </summary>
    public static void EnableDarkTitleBar(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            var useDarkMode = 1;
            DwmSetWindowAttribute(
                hwnd,
                DWMWA_USE_IMMERSIVE_DARK_MODE,
                ref useDarkMode,
                sizeof(int));
        };
    }
}
