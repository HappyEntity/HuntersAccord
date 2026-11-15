using System.Diagnostics;
using System.Security.Cryptography;

namespace HuntersAccord.Platform;

/// <summary>
/// Plays a WAV with a command-line player (Linux, macOS): the sound is written once to a temp file named by its
/// hash, then the first player that starts gets it. Failures are ignored — interface sounds are optional.
/// </summary>
internal static class SoundFile
{
    private static readonly Dictionary<string, string> Paths = [];
    private static string? _player;
    private static bool _noPlayer;

    public static void Play(byte[] wav, params string[] players)
    {
        if (_noPlayer) return;
        try
        {
            var path = PathFor(wav);
            foreach (var player in _player is { } known ? [known] : players)
            {
                try
                {
                    var psi = new ProcessStartInfo(player) { UseShellExecute = false, RedirectStandardError = true };
                    psi.ArgumentList.Add(path);
                    Process.Start(psi)?.Dispose();
                    _player = player;
                    return;
                }
                catch (System.ComponentModel.Win32Exception)
                {
                }
            }
            _noPlayer = true;
        }
        catch (IOException)
        {
        }
    }

    private static string PathFor(byte[] wav)
    {
        var key = Convert.ToHexString(SHA256.HashData(wav))[..16];
        lock (Paths)
        {
            if (Paths.TryGetValue(key, out var path)) return path;
            path = Path.Combine(Path.GetTempPath(), $"ha-sound-{key}.wav");
            if (!File.Exists(path)) File.WriteAllBytes(path, wav);
            Paths[key] = path;
            return path;
        }
    }
}
