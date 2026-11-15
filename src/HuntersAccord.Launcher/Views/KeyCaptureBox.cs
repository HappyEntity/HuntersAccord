using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using HuntersAccord.Launcher.Localization;

namespace HuntersAccord.Launcher.Views;

/// <summary>
/// A key set by pressing it (Esc or clicking elsewhere cancels). <see cref="Keys"/> holds the mod's key names
/// (LShift, LCtrl, A, F5...) joined by "+".
/// </summary>
public sealed class KeyCaptureBox : Button
{
    public static readonly StyledProperty<string> KeysProperty =
        AvaloniaProperty.Register<KeyCaptureBox, string>(nameof(Keys), "", defaultBindingMode: BindingMode.TwoWay);

    private static readonly string[] Modifiers = ["LCtrl", "RCtrl", "LShift", "RShift", "LAlt", "RAlt"];

    private readonly HashSet<string> _held = [];
    private readonly List<string> _combo = [];
    private bool _capturing;

    protected override Type StyleKeyOverride => typeof(Button);

    public string Keys
    {
        get => GetValue(KeysProperty);
        set => SetValue(KeysProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Localizer.Instance.LanguageChanged += OnLanguageChanged;
        Show();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Localizer.Instance.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Show();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == KeysProperty) Show();
    }

    protected override void OnClick()
    {
        base.OnClick();
        if (_capturing) return;
        _capturing = true;
        _held.Clear();
        _combo.Clear();
        Show();
        Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!_capturing)
        {
            base.OnKeyDown(e);
            return;
        }
        e.Handled = true;
        if (e.Key == Key.Escape)
        {
            Stop();
            return;
        }
        if (NameOf(e.Key) is not { } name) return;
        _held.Add(name);
        if (!_combo.Contains(name)) _combo.Add(name);
        Show();
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (!_capturing)
        {
            base.OnKeyUp(e);
            return;
        }
        e.Handled = true;
        if (NameOf(e.Key) is { } name) _held.Remove(name);
        if (_held.Count > 0 || _combo.Count == 0) return;
        // All let go: modifiers first, then the rest in the order pressed.
        var keys = string.Join("+", _combo.OrderBy(k => Array.IndexOf(Modifiers, k) is var i && i >= 0 ? i : Modifiers.Length));
        Stop();
        Keys = keys;
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (_capturing) Stop();
    }

    private void Stop()
    {
        _capturing = false;
        _held.Clear();
        _combo.Clear();
        Show();
    }

    private void Show()
    {
        Classes.Set("listen", _capturing);
        var keys = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        var shown = _capturing ? _combo : [.. Keys.Split('+', StringSplitOptions.RemoveEmptyEntries)];
        foreach (var key in shown)
        {
            if (keys.Children.Count > 0) keys.Children.Add(new TextBlock { Text = "+", Classes = { "keyplus" } });
            keys.Children.Add(new Border { Classes = { "keycap" }, Child = new TextBlock { Text = key } });
        }
        if (_capturing && shown.Count == 0)
            keys.Children.Add(new TextBlock { Text = Localizer.Instance["KeyCapture.Press"], VerticalAlignment = VerticalAlignment.Center });
        if (_capturing)
            keys.Children.Add(new Rectangle { Width = 1, Height = 16, Margin = new Thickness(3, 0, 0, 0), Fill = Brush("Gold300Brush") });

        var row = new DockPanel();
        if (_capturing)
        {
            var esc = new TextBlock
            {
                Text = "Esc", FontSize = 13, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brush("Ash500Brush"),
            };
            DockPanel.SetDock(esc, Dock.Right);
            row.Children.Add(esc);
        }
        row.Children.Add(keys);
        Content = row;
    }

    private IBrush? Brush(string key) => this.TryFindResource(key, out var value) ? value as IBrush : null;

    /// <summary>The mod's name of a key (WindowsInput's table), or null for keys it does not know.</summary>
    private static string? NameOf(Key key) => key switch
    {
        >= Key.A and <= Key.Z => key.ToString(),
        >= Key.D0 and <= Key.D9 => ((int)(key - Key.D0)).ToString(System.Globalization.CultureInfo.InvariantCulture),
        >= Key.F1 and <= Key.F12 => key.ToString(),
        Key.LeftShift => "LShift",
        Key.RightShift => "RShift",
        Key.LeftCtrl => "LCtrl",
        Key.RightCtrl => "RCtrl",
        Key.LeftAlt => "LAlt",
        Key.RightAlt => "RAlt",
        Key.Space => "Space",
        Key.Enter => "Enter",
        Key.Back => "Backspace",
        Key.Delete => "Delete",
        Key.Tab => "Tab",
        Key.Up => "Up",
        Key.Down => "Down",
        Key.Left => "Left",
        Key.Right => "Right",
        _ => null,
    };
}
