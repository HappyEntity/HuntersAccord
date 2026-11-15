using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HuntersAccord.Core.Emulator;

/// <summary>
/// Puts two game windows side by side (Windows only): player 1 on the left half of the work area, player 2 on the right.
/// </summary>
public static class GameWindows
{
    private const int WorkArea = 0x30; // SPI_GETWORKAREA
    private const uint NoZOrder = 0x4, NoActivate = 0x10;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    private static extern bool SystemParametersInfo(uint action, uint param, ref Rect rect, uint flags);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);

    /// <summary>
    /// Waits for the process's window (up to a minute) and places it in its half (0 left, 1 right), as tall as the game's frame of
    /// <paramref name="aspect"/> (width / height) needs at that width; false when it has none.
    /// </summary>
    public static bool PlaceHalf(Process process, int half, double aspect = 16.0 / 9)
    {
        if (!OperatingSystem.IsWindows()) return false;
        for (var i = 0; i < 60 && !process.HasExited; i++)
        {
            process.Refresh();
            if (process.MainWindowHandle != 0)
            {
                var area = new Rect();
                SystemParametersInfo(WorkArea, 0, ref area, 0);
                var width = (area.Right - area.Left) / 2;
                var height = Math.Min(area.Bottom - area.Top, (int)Math.Round(width / aspect) + 32);
                return SetWindowPos(process.MainWindowHandle, 0, area.Left + half * width, area.Top, width, height, NoZOrder | NoActivate);
            }
            Thread.Sleep(1000);
        }
        return false;
    }
}
