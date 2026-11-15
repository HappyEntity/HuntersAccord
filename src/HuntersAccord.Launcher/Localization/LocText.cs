using CommunityToolkit.Mvvm.ComponentModel;

namespace HuntersAccord.Launcher.Localization;

/// <summary>A localized message kept as key + arguments, so it re-translates when the language changes.</summary>
public sealed class LocText : ObservableObject
{
    private string _key;
    private object?[] _args;

    public LocText(string key, params object?[] args)
    {
        _key = key;
        _args = args;
        Localizer.Instance.LanguageChanged += (_, _) => OnPropertyChanged(nameof(Text));
    }

    public string Text => Localizer.Instance.Format(_key, _args.Select(a => a is LocText nested ? nested.Text : a).ToArray());

    public void Set(string key, params object?[] args)
    {
        _key = key;
        _args = args;
        OnPropertyChanged(nameof(Text));
    }

    public override string ToString() => Text;
}
