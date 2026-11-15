using HuntersAccord.Core.Game;

namespace HuntersAccord.Core;

/// <summary>What the launcher gives the mod when it starts the game (environment variables), and where the game's mods folder is.</summary>
public static class ModContract
{
    /// <summary>Default port of the co-op session directory on localhost (served by the mod library in the launcher).</summary>
    public const int DirectoryPort = 18670;

    public const string DirectoryAddressVariable = "HA_SESSION_DIRECTORY";

    public const string PlayerVariable = "HA_PLAYER";

    /// <summary>Environment variable that moves the local directory to another port.</summary>
    public const string DirectoryPortVariable = "HA_SESSION_PORT";

    public const string ModsDirVariable = "HA_GAME_MODS";

    public static int DirectoryLocalPort => DirectoryPortFor(Environment.GetEnvironmentVariable(DirectoryPortVariable));

    public static int DirectoryPortFor(string? variable) =>
        int.TryParse(variable, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var port)
        && port is > 0 and <= 65535
            ? port
            : DirectoryPort;

    public static string DirectoryLocalAddress => $"http://localhost:{DirectoryLocalPort}/";

    /// <summary>shadPS4's mods folder of a game (<c>&lt;game&gt;-mods</c>): where the files Hunters' Accord makes go.</summary>
    public static string ModsDir(GameInstall game) => game.BasePath.TrimEnd('\\', '/') + "-mods";
}
