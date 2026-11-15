namespace HuntersAccord.Platform.Linux;

/// <summary>Native Linux and Steam Deck. Not tested yet — paths follow Steam/shadPS4 defaults.</summary>
internal sealed class LinuxPlatformServices : IPlatformServices
{
    private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public string? FindSteamRoot()
    {
        string[] candidates =
        [
            Path.Combine(Home, ".steam", "steam"),
            Path.Combine(Home, ".local", "share", "Steam"),
            Path.Combine(Home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"), // Flatpak
        ];
        return candidates.FirstOrDefault(Directory.Exists);
    }

    public uint? SteamActiveAccount() => null;

    public void OpenFolder(string path)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("xdg-open") { UseShellExecute = false };
        psi.ArgumentList.Add(Path.GetFullPath(path));
        System.Diagnostics.Process.Start(psi)?.Dispose();
    }

    public void RevealFile(string path) => OpenFolder(Path.GetDirectoryName(Path.GetFullPath(path))!);

    public void OpenUrl(string url)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("xdg-open") { UseShellExecute = false };
        psi.ArgumentList.Add(url);
        System.Diagnostics.Process.Start(psi)?.Dispose();
    }

    public void PlaySound(byte[] wav) => SoundFile.Play(wav, "paplay", "pw-play", "aplay");

    public void KeepSoundReady() { }

    public string GetShadPs4UserDir()
    {
        var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        return Path.Combine(string.IsNullOrEmpty(xdg) ? Path.Combine(Home, ".local", "share") : xdg, "shadPS4");
    }
}
