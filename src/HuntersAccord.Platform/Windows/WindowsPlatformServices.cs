using System.Runtime.Versioning;
using Microsoft.Win32;

namespace HuntersAccord.Platform.Windows;

[SupportedOSPlatform("windows")]
internal sealed class WindowsPlatformServices : IPlatformServices
{
    public string? FindSteamRoot()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        if (key?.GetValue("SteamPath") is string path && Directory.Exists(path))
            return Path.GetFullPath(path);

        var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
        return Directory.Exists(fallback) ? fallback : null;
    }

    public uint? SteamActiveAccount()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess");
        return key?.GetValue("ActiveUser") is int user ? unchecked((uint)user) : 0;
    }

    public string GetShadPs4UserDir() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "shadPS4");

    public void OpenFolder(string path)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("explorer.exe") { UseShellExecute = false };
        psi.ArgumentList.Add(Path.GetFullPath(path));
        System.Diagnostics.Process.Start(psi)?.Dispose();
    }

    public void RevealFile(string path) =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe")
        {
            UseShellExecute = false,
            Arguments = $"/select,\"{Path.GetFullPath(path)}\"",
        })?.Dispose();

    public void OpenUrl(string url) =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();

    private readonly WaveOutMixer _sounds = new();

    public void PlaySound(byte[] wav) => _sounds.Play(wav);

    public void KeepSoundReady() => _sounds.KeepReady();
}
