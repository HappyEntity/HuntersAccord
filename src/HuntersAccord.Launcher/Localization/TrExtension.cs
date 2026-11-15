using System.Diagnostics.CodeAnalysis;
using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace HuntersAccord.Launcher.Localization;

/// <summary>XAML shorthand for a UI string that follows the selected language: <c>{loc:Tr Game.Header}</c>.</summary>
public sealed class TrExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(LocText))]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "LocText.Text is preserved by DynamicDependency.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Binding to a plain CLR property; no generic instantiation needed.")]
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding(nameof(LocText.Text)) { Source = new LocText(Key), Mode = BindingMode.OneWay };
}
