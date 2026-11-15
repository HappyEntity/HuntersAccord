namespace HuntersAccord.Platform.MacOS;

/// <summary>macOS. Not tested yet — paths follow Steam/shadPS4 defaults.</summary>
internal sealed class MacPlatformServices : IPlatformServices
{
    private static string AppSupport => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support");

    public string? FindSteamRoot()
    {
        var path = Path.Combine(AppSupport, "Steam");
        return Directory.Exists(path) ? path : null;
    }

    public string GetShadPs4UserDir() => Path.Combine(AppSupport, "shadPS4");

    public uint? SteamActiveAccount() => null;

    public void OpenFolder(string path)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("open") { UseShellExecute = false };
        psi.ArgumentList.Add(Path.GetFullPath(path));
        System.Diagnostics.Process.Start(psi)?.Dispose();
    }

    public void RevealFile(string path)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("open") { UseShellExecute = false };
        psi.ArgumentList.Add("-R");
        psi.ArgumentList.Add(Path.GetFullPath(path));
        System.Diagnostics.Process.Start(psi)?.Dispose();
    }

    public void OpenUrl(string url)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("open") { UseShellExecute = false };
        psi.ArgumentList.Add(url);
        System.Diagnostics.Process.Start(psi)?.Dispose();
    }

    public void PlaySound(byte[] wav) => SoundFile.Play(wav, "afplay");

    public void KeepSoundReady() { }
}
