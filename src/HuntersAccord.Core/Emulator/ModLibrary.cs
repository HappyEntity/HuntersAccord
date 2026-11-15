using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using HuntersAccord.Core.Game;

namespace HuntersAccord.Core.Emulator;

/// <summary>
/// The launcher's way into the mod library (the Vulkan layer in <c>mod/</c>): loaded in-process and called through one C entry point,
/// <c>ha_launcher_call</c>, with a JSON request and reply. Without it (a build from the public source alone) the game starts as shadPS4 alone would.
/// </summary>
public sealed unsafe class ModLibrary
{
    /// <summary>The launcher API this launcher speaks (<c>ha_launcher_api</c> must answer it).</summary>
    public const int ApiVersion = 1;

    private const int ReplyCapacity = 64 * 1024;

    private static readonly Lock Gate = new();
    private static ModLibrary? _loaded;
    private static bool _tried;

    private readonly delegate* unmanaged[Cdecl]<byte*, byte*, int, int> _call;

    private ModLibrary(delegate* unmanaged[Cdecl]<byte*, byte*, int, int> call) => _call = call;

    public static string FileName =>
        OperatingSystem.IsWindows() ? "huntersaccord.dll" : OperatingSystem.IsMacOS() ? "libhuntersaccord.dylib" : "libhuntersaccord.so";

    /// <summary>The mod library next to the launcher, loaded once; null when absent or speaking another API version.</summary>
    public static ModLibrary? Find()
    {
        lock (Gate)
        {
            if (_tried) return _loaded;
            _tried = true;
            if (GameLauncher.FindModLayerDir() is not { } dir) return null;
            _loaded = Load(Path.Combine(dir, FileName));
            return _loaded;
        }
    }

    /// <summary>Loads a mod library from a path; null when it is missing, cannot be loaded or speaks another API.</summary>
    public static ModLibrary? Load(string path)
    {
        if (!File.Exists(path) || !NativeLibrary.TryLoad(path, out var handle)) return null;
        if (!NativeLibrary.TryGetExport(handle, "ha_launcher_api", out var api)
            || !NativeLibrary.TryGetExport(handle, "ha_launcher_call", out var call)) return null;
        if (((delegate* unmanaged[Cdecl]<int>)api)() != ApiVersion) return null;
        return new ModLibrary((delegate* unmanaged[Cdecl]<byte*, byte*, int, int>)call);
    }

    public bool StartSessionDirectory() => Call(new ModLibraryRequest { Op = ModLibraryRequest.StartDirectory }).Serving;

    /// <summary>
    /// Writes the game files Hunters' Accord overrides into the game's mods folder (skipped when nothing changed).
    /// <paramref name="texts"/>: the launcher's translations, language code → key → text (English as "en").
    /// <paramref name="patches"/>: the keys of the patches the game starts with, in order (listed on the title screen).
    /// </summary>
    public void BuildModFiles(GameInstall game, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> texts,
        IEnumerable<string> patches, bool freeGameBells, bool keyPrompts, bool skipLogos) =>
        Check(Call(new ModLibraryRequest
        {
            Op = ModLibraryRequest.BuildModFiles,
            Game = game,
            Texts = texts.ToDictionary(t => t.Key, t => t.Value.ToDictionary(), StringComparer.OrdinalIgnoreCase),
            Patches = [.. patches],
            FreeGameBells = freeGameBells,
            KeyPrompts = keyPrompts,
            SkipLogos = skipLogos,
        }));

    /// <summary>Turns Hunters' Accord's files in the game's mods folder off (start without the mod) or back on.</summary>
    public void SetModFilesEnabled(GameInstall game, bool enabled) =>
        Check(Call(new ModLibraryRequest { Op = ModLibraryRequest.SetModFilesEnabled, Game = game, Enabled = enabled }));

    private ModLibraryReply Call(ModLibraryRequest request)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(request, ModLibraryJson.Default.ModLibraryRequest);
        var input = new byte[json.Length + 1];
        json.CopyTo(input, 0);
        var output = new byte[ReplyCapacity];
        int length;
        fixed (byte* i = input)
        fixed (byte* o = output)
            length = _call(i, o, output.Length);
        if (length <= 0 || length > output.Length) throw new InvalidDataException("The mod library gave no answer.");
        return JsonSerializer.Deserialize(output.AsSpan(0, length), ModLibraryJson.Default.ModLibraryReply)
               ?? throw new InvalidDataException("The mod library gave no answer.");
    }

    private static void Check(ModLibraryReply reply)
    {
        if (reply.Ok) return;
        var message = reply.Error ?? "The mod library failed.";
        throw reply.ErrorKind switch
        {
            ModLibraryReply.FileMissing => new FileNotFoundException(message),
            ModLibraryReply.DirectoryMissing => new DirectoryNotFoundException(message),
            ModLibraryReply.NoAccess => new UnauthorizedAccessException(message),
            ModLibraryReply.BadData => new InvalidDataException(message),
            ModLibraryReply.Io => new IOException(message, reply.HResult),
            _ => new InvalidOperationException(message),
        };
    }
}

public sealed class ModLibraryRequest
{
    public const string StartDirectory = "directory.start";
    public const string BuildModFiles = "modfiles.build";
    public const string SetModFilesEnabled = "modfiles.enabled";

    public string Op { get; set; } = "";
    public GameInstall? Game { get; set; }
    public Dictionary<string, Dictionary<string, string>>? Texts { get; set; }
    public List<string>? Patches { get; set; }
    public bool FreeGameBells { get; set; }
    public bool KeyPrompts { get; set; } = true;
    public bool SkipLogos { get; set; } = true;
    public bool Enabled { get; set; }
}

public sealed class ModLibraryReply
{
    public const string FileMissing = "file";
    public const string DirectoryMissing = "directory";
    public const string NoAccess = "access";
    public const string BadData = "data";
    public const string Io = "io";
    public const string Other = "other";

    public bool Ok { get; set; }
    public bool Serving { get; set; }
    public string? Error { get; set; }
    public string? ErrorKind { get; set; }
    public int HResult { get; set; }

    public static ModLibraryReply Failed(Exception e) => new()
    {
        Error = e.Message,
        HResult = e.HResult,
        ErrorKind = e switch
        {
            FileNotFoundException => FileMissing,
            DirectoryNotFoundException => DirectoryMissing,
            UnauthorizedAccessException => NoAccess,
            InvalidDataException => BadData,
            IOException => Io,
            _ => Other,
        },
    };
}

[JsonSerializable(typeof(ModLibraryRequest))]
[JsonSerializable(typeof(ModLibraryReply))]
public sealed partial class ModLibraryJson : JsonSerializerContext;
