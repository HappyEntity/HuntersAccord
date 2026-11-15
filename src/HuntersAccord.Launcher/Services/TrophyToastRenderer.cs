using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using HuntersAccord.Core;
using HuntersAccord.Core.Game;
using HuntersAccord.Core.Trophies;
using HuntersAccord.Launcher.Localization;
using HuntersAccord.Launcher.ViewModels;
using HuntersAccord.Launcher.Views;

namespace HuntersAccord.Launcher.Services;

/// <summary>
/// The in-game trophy notices (design v9, cards Toast01–Toast04), drawn here with the launcher's fonts in its language
/// and kept as PNGs (<see cref="TrophyToastFiles"/>) for the game's overlay, which only lays a ready picture over the
/// frame (with its own fade, slide and the platinum's glow). Drawn at 1080, 1440 and 2160 lines (everything × height/1080,
/// transparent margins of 32 × that for the shadow and the glow), so the overlay never stretches them.
/// <para>
/// The co-op line ("in co-op: Nika"): the partner's name is only known at the award, in the game. So each trophy also
/// has a co-op picture with the line's start drawn and room left for the name (its place in a .txt beside it), and the
/// line's characters are drawn once per height (toast-glyphs-&lt;h&gt;.png/.txt): the overlay writes the name there from
/// them at once, with no round trip to the launcher (which may not even be running).
/// </para>
/// The picture: the trophy's own from the player's game copy (<see cref="TrophyPictures"/>) with its rank badge in the
/// corner, else the rank badge alone. The trophy's name: the game's own in the game's language when the player's trophy key
/// opens it (<see cref="TrophyTextSource"/>), else ours. Made again when the language, the game copy's pack, the words
/// (key or game language) or <see cref="LayoutVersion"/> changed (a stamp file beside them).
/// </summary>
public static class TrophyToastRenderer
{
    /// <summary>Raised when the look changes: every notice is drawn again.</summary>
    public const int LayoutVersion = 4;

    private const string StampName = "stamp.txt";

    /// <summary>The notices for every trophy in <paramref name="language"/>, unless they are there already (UI thread).</summary>
    public static void Ensure(string language, GameInstall? game = null)
    {
        var dataRoot = AppPaths.DataRoot;
        var folder = TrophyBook.ToastFolderIn(dataRoot);
        var pack = game is null ? null : TrophyPack.Find(game.BasePath, game.UpdatePath);
        var packStamp = pack is null ? "none" : $"{pack}|{new FileInfo(pack).Length}";
        // The words: ours, or the game's own in its language (TrophyTextSource; the stamp names which, never a key or a text).
        var words = TrophyTextSource.Instance;
        var stamp = $"{LayoutVersion} {language} {packStamp} {words.Stamp}";
        var stampFile = Path.Combine(folder, StampName);
        try
        {
            if (File.Exists(stampFile) && File.ReadAllText(stampFile).Trim() == stamp
                && TrophyCatalog.All.All(t => File.Exists(TrophyToastFiles.Picture(dataRoot, t.Id, TrophyToastFiles.Heights[^1], coop: true))))
                return;
            Directory.CreateDirectory(folder);
            foreach (var old in Directory.EnumerateFiles(folder, "trophy-toast-*")) File.Delete(old);
            TrophyPictures.Ensure(dataRoot, pack);
            foreach (var height in TrophyToastFiles.Heights)
            {
                var glyphs = DrawGlyphs(language, height, out var info);
                Save(glyphs, TrophyToastFiles.GlyphsPicture(dataRoot, height));
                File.WriteAllText(TrophyToastFiles.GlyphsInfo(dataRoot, height), info.Write());
                foreach (var trophy in words.All)
                {
                    var picture = Picture(dataRoot, trophy.Id);
                    foreach (var coop in new[] { false, true })
                    {
                        var drawn = Draw(trophy, language, height, coop, picture);
                        Save(drawn.Notice, TrophyToastFiles.Picture(dataRoot, trophy.Id, height, coop));
                        if (drawn.Glow is { } glow) Save(glow, TrophyToastFiles.Picture(dataRoot, trophy.Id, height, coop, glow: true));
                        if (drawn.Slot is { } slot) File.WriteAllText(TrophyToastFiles.CoopSlotFile(dataRoot, trophy.Id, height), slot.Write());
                    }
                    (picture as IDisposable)?.Dispose();
                }
            }
            File.WriteAllText(stampFile, stamp);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Without them the game shows its own plain notice: nothing to stop the start for.
        }
    }

    private static void Save(RenderTargetBitmap bitmap, string path)
    {
        using (bitmap)
        {
            bitmap.Save(path + ".tmp", PngBitmapEncoderOptions.Default);
            File.Move(path + ".tmp", path, overwrite: true);
        }
    }

    private static Bitmap? Picture(string dataRoot, int id)
    {
        if (TrophyPictures.Find(dataRoot, id) is not { } file) return null;
        try
        {
            return new Bitmap(file);
        }
        catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    // The design at 1080 lines (×scale): trophies.md, "Уведомление в игре".
    private const double PlateWidth = 440, PadTop = 14, PadRight = 22, PadBottom = 14, PadLeft = 16, Icon = 64, IconGap = 16;
    private const double Corner = 14, LabelSize = 13, NameSize = 21, NameLine = 26, CoopSize = 14;

    private static readonly FontFamily Caps = new("avares://HuntersAccord/Assets/Fonts#Cormorant SC");
    private static readonly FontFamily Text = new("avares://HuntersAccord/Assets/Fonts#EB Garamond");
    private static readonly Color Gold700 = Color.Parse("#8A7550");
    private static readonly Color Gold500 = Color.Parse("#B49B6C");
    private static readonly Color Gold300 = Color.Parse("#D8C49A");
    private static readonly Color Bone = Color.Parse("#E8DFCF");
    private static readonly Color PlatinumName = Color.Parse("#F4ECDC");
    private static readonly Color Ash300 = Color.Parse("#A89D90");

    private static CultureInfo Culture(string language) =>
        CultureInfo.GetCultureInfo(language.StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? "ru-RU" : "en-US");

    private static TextLayout Layout(string text, FontFamily family, FontWeight weight, double size, Color color,
        double maxWidth = double.PositiveInfinity, double lineHeight = double.NaN, double letterSpacing = 0, int maxLines = 0) =>
        new(text, new Typeface(family, FontStyle.Normal, weight), size, new SolidColorBrush(color), TextAlignment.Left,
            maxLines == 1 ? TextWrapping.NoWrap : TextWrapping.Wrap, TextTrimming.CharacterEllipsis, null, FlowDirection.LeftToRight,
            maxWidth, double.PositiveInfinity, lineHeight, letterSpacing, maxLines);

    /// <summary>The co-op line's start in the language ("in co-op: " of "in co-op: {0}").</summary>
    private static string CoopPrefix(string language)
    {
        var format = Localizer.Instance.Get(language, "Trophies.Coop");
        var at = format.IndexOf("{0}", StringComparison.Ordinal);
        return at < 0 ? format + " " : format[..at];
    }

    /// <summary>A notice drawn: the picture, the platinum's glow (null for the others), the co-op name's place.</summary>
    public sealed record Drawn(RenderTargetBitmap Notice, RenderTargetBitmap? Glow, CoopSlot? Slot);

    /// <summary>One notice at a frame height; <paramref name="coop"/>: with the co-op line's start and room for the name.</summary>
    public static Drawn Draw(TrophyInfo trophy, string language, int height, bool coop, IImage? picture)
    {
        var s = TrophyToastFiles.Scale(height);
        var margin = TrophyToastFiles.Margin(height);
        var platinum = trophy.Grade == TrophyGrade.Platinum;
        var textLeft = (PadLeft + Icon + IconGap) * s;
        var textWidth = PlateWidth * s - textLeft - PadRight * s;

        var label = Layout(Localizer.Instance.Get(language, platinum ? "Trophy.Toast.Platinum" : "Trophy.Toast.Earned"),
            Caps, FontWeight.Bold, LabelSize * s, platinum ? Gold300 : Gold500, lineHeight: LabelSize * 1.3 * s,
            letterSpacing: LabelSize * 0.2 * s, maxLines: 1);
        var nameText = trophy.Name(language);
        var name = Layout(nameText, Text, FontWeight.Medium, NameSize * s, platinum ? PlatinumName : Bone, textWidth,
            NameLine * s, maxLines: 2);
        var coopPrefix = coop ? Layout(CoopPrefix(language), Text, FontWeight.Normal, CoopSize * s, Ash300, lineHeight: CoopSize * 1.3 * s, maxLines: 1) : null;

        var nameTop = label.Height + 0.15 * NameSize * s;
        var coopTop = nameTop + name.Height + 0.3 * CoopSize * s;
        var textHeight = coopPrefix is null ? nameTop + name.Height : coopTop + coopPrefix.Height;
        var plateHeight = Math.Round(Math.Max(Icon * s, textHeight) + (PadTop + PadBottom) * s);
        var plateWidth = Math.Round(PlateWidth * s);
        var width = (int)plateWidth + 2 * margin;
        var full = (int)plateHeight + 2 * margin;
        var plate = new Rect(margin, margin, plateWidth, plateHeight);

        var bitmap = new RenderTargetBitmap(new PixelSize(width, full), new Vector(96, 96));
        CoopSlot? slot = null;
        using (var g = bitmap.CreateDrawingContext())
        {
            var fill = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops = platinum
                    ? [new GradientStop(Color.FromArgb(242, 20, 16, 25), 0), new GradientStop(Color.FromArgb(230, 42, 34, 41), 1)]
                    : [new GradientStop(Color.FromArgb(240, 12, 10, 14), 0), new GradientStop(Color.FromArgb(224, 20, 16, 25), 1)],
            };
            var shadow = new BoxShadows(new BoxShadow { OffsetX = 0, OffsetY = 6 * s, Blur = 24 * s, Color = Color.FromArgb(140, 0, 0, 0) });
            var hair = Math.Max(1, Math.Round(s));
            var rim = new Pen(new SolidColorBrush(platinum ? Gold300 : Color.FromArgb(217, Gold700.R, Gold700.G, Gold700.B)), hair);
            g.DrawRectangle(fill, null, new RoundedRect(plate), shadow);
            g.DrawRectangle(null, rim, plate.Deflate(hair / 2));
            // The corners: light gold, top left and bottom right, over the rim.
            var cornerPen = new Pen(new SolidColorBrush(Gold300), hair);
            var c = Math.Round(Corner * s);
            var h = hair / 2;
            g.DrawGeometry(null, cornerPen, Geometry.Parse(FormattableString.Invariant(
                $"M{plate.X + h},{plate.Y + c} V{plate.Y + h} H{plate.X + c}")));
            g.DrawGeometry(null, cornerPen, Geometry.Parse(FormattableString.Invariant(
                $"M{plate.Right - c},{plate.Bottom - h} H{plate.Right - h} V{plate.Bottom - c}")));

            var icon = new Rect(plate.X + PadLeft * s, plate.Y + (plateHeight - Icon * s) / 2, Icon * s, Icon * s);
            TrophyIcon.Paint(g, icon, picture, TrophyArt.Badge(trophy.Grade, true, false), false);

            var top = plate.Y + (plateHeight - textHeight) / 2;
            var x = plate.X + textLeft;
            label.Draw(g, new Point(x, top));
            name.Draw(g, new Point(x, top + nameTop));
            if (coopPrefix is not null)
            {
                var lineTop = top + coopTop;
                var d = 0.38 * CoopSize * s;
                var cy = lineTop + coopPrefix.Height / 2 + 0.5 * s;
                g.DrawGeometry(new SolidColorBrush(Gold500), null, Geometry.Parse(FormattableString.Invariant(
                    $"M{x + d * 0.7},{cy - d * 0.7} L{x + d * 1.4},{cy} L{x + d * 0.7},{cy + d * 0.7} L{x},{cy} Z")));
                var prefixX = x + d * 1.4 + 0.55 * CoopSize * s;
                coopPrefix.Draw(g, new Point(prefixX, lineTop));
                var nameX = prefixX + coopPrefix.WidthIncludingTrailingWhitespace;
                slot = new CoopSlot((int)Math.Round(nameX), (int)Math.Round(lineTop), (int)Math.Round(plate.Right - PadRight * s));
            }
        }

        RenderTargetBitmap? glowBitmap = null;
        if (platinum)
        {
            // The platinum's glow: its own layer, laid under the plate by the overlay with its own fade.
            glowBitmap = new RenderTargetBitmap(new PixelSize(width, full), new Vector(96, 96));
            using var g = glowBitmap.CreateDrawingContext();
            var area = plate.Inflate(24 * s);
            var glow = new RadialGradientBrush
            {
                Center = new RelativePoint(0.18, 0.5, RelativeUnit.Relative),
                GradientOrigin = new RelativePoint(0.18, 0.5, RelativeUnit.Relative),
                RadiusX = new RelativeScalar(0.82, RelativeUnit.Relative),
                RadiusY = new RelativeScalar(0.9, RelativeUnit.Relative),
                GradientStops = [new GradientStop(Color.FromArgb(97, Gold300.R, Gold300.G, Gold300.B), 0), new GradientStop(Color.FromArgb(0, Gold300.R, Gold300.G, Gold300.B), 0.62)],
            };
            // Faded out towards every edge of its area: no straight edge where the area ends (the frame shows through).
            static LinearGradientBrush Fade(bool horizontal) => new()
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(horizontal ? 1 : 0, horizontal ? 0 : 1, RelativeUnit.Relative),
                GradientStops = [new GradientStop(Colors.Transparent, 0), new GradientStop(Colors.White, 0.22),
                                 new GradientStop(Colors.White, 0.78), new GradientStop(Colors.Transparent, 1)],
            };
            using (g.PushOpacityMask(Fade(true), area))
            using (g.PushOpacityMask(Fade(false), area))
                g.DrawRectangle(glow, null, area);
        }
        return new Drawn(bitmap, glowBitmap, slot);
    }

    /// <summary>The co-op line's characters at a height (the line's font and colour), one cell each, for the overlay.</summary>
    public static RenderTargetBitmap DrawGlyphs(string language, int height, out ToastGlyphs info)
    {
        var s = TrophyToastFiles.Scale(height);
        var size = CoopSize * s;
        var lineHeight = (int)Math.Ceiling(size * 1.3);
        var pad = (int)Math.Ceiling(2 * s);
        const int atlasWidth = 1024;
        var cells = new List<(int Code, TextLayout Layout, int Advance, int Width)>();
        foreach (var code in ToastGlyphs.Set())
        {
            var text = char.ConvertFromUtf32(code);
            var layout = Layout(text, Text, FontWeight.Normal, size, Ash300, lineHeight: size * 1.3, maxLines: 1);
            var advance = (int)Math.Round(layout.WidthIncludingTrailingWhitespace);
            cells.Add((code, layout, advance, advance + 2 * pad + 1));
        }
        var glyphs = new Dictionary<int, ToastGlyph>();
        int x = 0, y = 0;
        foreach (var cell in cells)
        {
            if (x + cell.Width > atlasWidth)
            {
                x = 0;
                y += lineHeight;
            }
            glyphs[cell.Code] = new ToastGlyph(x, y, cell.Width, lineHeight, cell.Advance, pad);
            x += cell.Width;
        }
        var bitmap = new RenderTargetBitmap(new PixelSize(atlasWidth, y + lineHeight), new Vector(96, 96));
        using (var g = bitmap.CreateDrawingContext())
            foreach (var cell in cells)
            {
                var at = glyphs[cell.Code];
                using (g.PushClip(new Rect(at.X, at.Y, at.Width, at.Height)))
                    cell.Layout.Draw(g, new Point(at.X + pad, at.Y));
            }
        info = new ToastGlyphs(lineHeight, glyphs);
        return bitmap;
    }
}
