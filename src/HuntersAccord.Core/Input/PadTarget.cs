namespace HuntersAccord.Core.Input;

/// <summary>
/// What a key can drive on the virtual PS4 pad: a button (bit in OrbisPadData.buttons) or one direction of a stick.
/// Names are stable identifiers used in settings files.
/// Touchpad targets also touch the pad at <see cref="TouchX"/> (games tell its left and right half apart).
/// </summary>
public sealed record PadTarget(string Id, uint ButtonMask = 0, StickDirection? Stick = null, ushort? TouchX = null)
{
    public bool IsButton => ButtonMask != 0;
}

public enum StickDirection
{
    LeftUp, LeftDown, LeftLeft, LeftRight,
    RightUp, RightDown, RightLeft, RightRight,
}

public static class PadTargets
{
    // OrbisPadButtonDataOffset bits.
    public const uint L3 = 0x2, R3 = 0x4, Options = 0x8, Up = 0x10, Right = 0x20, Down = 0x40, Left = 0x80;
    public const uint L2 = 0x100, R2 = 0x200, L1 = 0x400, R1 = 0x800;
    public const uint Triangle = 0x1000, Circle = 0x2000, Cross = 0x4000, Square = 0x8000, TouchPad = 0x100000;

    /// <summary>Touchpad size scePadGetControllerInformation reports in shadPS4.</summary>
    public const int TouchPadWidth = 1920, TouchPadHeight = 942;

    public static IReadOnlyList<PadTarget> All { get; } =
    [
        new("cross", Cross), new("circle", Circle), new("square", Square), new("triangle", Triangle),
        new("l1", L1), new("r1", R1), new("l2", L2), new("r2", R2), new("l3", L3), new("r3", R3),
        new("options", Options), new("touchpad", TouchPad, TouchX: TouchPadWidth / 2),
        new("touchpad_left", TouchPad, TouchX: TouchPadWidth / 4), new("touchpad_right", TouchPad, TouchX: TouchPadWidth * 3 / 4),
        new("dpad_up", Up), new("dpad_down", Down), new("dpad_left", Left), new("dpad_right", Right),
        new("move_forward", Stick: StickDirection.LeftUp), new("move_back", Stick: StickDirection.LeftDown),
        new("move_left", Stick: StickDirection.LeftLeft), new("move_right", Stick: StickDirection.LeftRight),
        new("camera_up", Stick: StickDirection.RightUp), new("camera_down", Stick: StickDirection.RightDown),
        new("camera_left", Stick: StickDirection.RightLeft), new("camera_right", Stick: StickDirection.RightRight),
    ];

    public static PadTarget? Find(string id) => All.FirstOrDefault(t => t.Id == id);
}
