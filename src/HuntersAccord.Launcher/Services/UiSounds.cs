using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using HuntersAccord.Platform;

namespace HuntersAccord.Launcher.Services;

/// <summary>Click feedback synthesized in code (no sound files): ticks for buttons and tabs, a bell toll for Play.</summary>
public static class UiSounds
{
    private const int Rate = 44100;

    public static bool Enabled { get; set; } = true;

    private static readonly Lazy<byte[]> Tick = new(() => Wav(0.08, t => Knock(t, 520, 170)));
    private static readonly Lazy<byte[]> TickOn = new(() => Wav(0.08, t => Knock(t, 620, 200)));
    private static readonly Lazy<byte[]> TickOff = new(() => Wav(0.08, t => Knock(t, 440, 140)));
    private static readonly Lazy<byte[]> Toll = new(() => Wav(1.8, Bell));

    public static void Attach()
    {
        // Sound output wakes up ahead of the first click: at start and whenever the pointer comes over a window.
        Warm();
        InputElement.PointerEnteredEvent.AddClassHandler<Window>((_, _) => Warm());
        Button.ClickEvent.AddClassHandler<Button>((button, _) => OnClick(button));
        InputElement.PointerPressedEvent.AddClassHandler<TabItem>((tab, _) =>
        {
            if (!tab.IsSelected && tab.IsEnabled) Play(Tick);
        }, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        InputElement.PointerPressedEvent.AddClassHandler<ListBoxItem>((item, _) =>
        {
            if (item.IsEnabled) Play(Tick);
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        InputElement.PointerPressedEvent.AddClassHandler<ComboBox>((box, _) =>
        {
            if (box.IsEnabled && !box.IsDropDownOpen) Play(Tick);
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    public static void Warm()
    {
        if (!Enabled) return;
        try
        {
            PlatformServices.Current.KeepSoundReady();
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or PlatformNotSupportedException)
        {
            Enabled = false;
        }
    }

    private static void OnClick(Button button)
    {
        if (button is ToggleButton toggle) Play(toggle.IsChecked == true ? TickOn : TickOff);
        else if (button.Classes.Contains("play")) Play(Toll);
        else Play(Tick);
    }

    private static void Play(Lazy<byte[]> sound)
    {
        if (!Enabled) return;
        try
        {
            PlatformServices.Current.PlaySound(sound.Value);
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or PlatformNotSupportedException)
        {
            Enabled = false;
        }
    }

    private static double Knock(double t, double high, double low) =>
        0.09 * (0.35 * Math.Sin(2 * Math.PI * high * t) * Math.Exp(-t / 0.012)
                + 0.65 * Math.Sin(2 * Math.PI * low * t) * Math.Exp(-t / 0.03));

    // Partials of a large bell (hum, prime, minor third, fifth, octave and upper ones): ratio, level, decay (s).
    private static readonly (double Ratio, double Level, double Decay)[] BellPartials =
    [
        (0.5, 0.35, 2.4), (1.0, 1.0, 1.5), (1.2, 0.45, 1.1), (1.5, 0.3, 0.9), (2.0, 0.4, 0.75), (2.74, 0.08, 0.35),
    ];

    private static double Bell(double t)
    {
        const double f = 98;
        var sum = 0.0;
        foreach (var (ratio, level, decay) in BellPartials)
        {
            var env = Math.Exp(-t / decay);
            sum += level * env * (Math.Sin(2 * Math.PI * f * ratio * t) + 0.5 * Math.Sin(2 * Math.PI * f * ratio * 1.003 * t));
        }
        return 0.05 * sum;
    }

    private static byte[] Wav(double seconds, Func<double, double> sample)
    {
        var count = (int)(seconds * Rate);
        var wav = new byte[44 + count * 2];
        using var w = new BinaryWriter(new MemoryStream(wav));
        w.Write("RIFF"u8); w.Write(36 + count * 2); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(Rate); w.Write(Rate * 2);
        w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(count * 2);
        for (var i = 0; i < count; i++)
        {
            var t = (double)i / Rate;
            var fade = Math.Min(1, t / 0.004) * Math.Min(1, (seconds - t) / 0.02); // no clicks at the edges
            w.Write((short)Math.Clamp(sample(t) * fade * short.MaxValue, short.MinValue, short.MaxValue));
        }
        return wav;
    }
}
