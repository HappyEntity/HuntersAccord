namespace HuntersAccord.Platform;

/// <summary>
/// Everything that differs between Windows, Linux (native or Proton) and macOS.
/// Game logic never branches on the OS itself — it asks this interface.
/// </summary>
public interface IPlatformServices
{
    /// <summary>Steam client install folder, or null when Steam is not installed.</summary>
    string? FindSteamRoot();

    /// <summary>
    /// The account signed in to the running Steam client (its 32-bit account id): 0 when Steam runs signed out or is not
    /// running; null when this OS cannot tell (then the launcher goes by the Steam process alone).
    /// </summary>
    uint? SteamActiveAccount();

    /// <summary>Folder where shadPS4 keeps its user data (config.json, input_config, patches...).</summary>
    string GetShadPs4UserDir();

    void OpenFolder(string path);

    /// <summary>Shows <paramref name="path"/> (a file) in the file manager, selected where the OS can; else opens its folder.</summary>
    void RevealFile(string path);

    void OpenUrl(string url);

    /// <summary>Plays a short 16-bit mono 44.1 kHz WAV without waiting. Silently does nothing when it cannot.</summary>
    void PlaySound(byte[] wav);

    /// <summary>Gets sound output ready ahead of the first sound (a waking audio device would make it late).</summary>
    void KeepSoundReady();
}

public static class PlatformServices
{
    public static IPlatformServices Current { get; } = Create();

    private static IPlatformServices Create()
    {
        if (OperatingSystem.IsWindows()) return new Windows.WindowsPlatformServices();
        if (OperatingSystem.IsLinux()) return new Linux.LinuxPlatformServices();
        if (OperatingSystem.IsMacOS()) return new MacOS.MacPlatformServices();
        throw new PlatformNotSupportedException();
    }
}
