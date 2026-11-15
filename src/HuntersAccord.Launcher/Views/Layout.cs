using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace HuntersAccord.Launcher.Views;

/// <summary>
/// A settings field (design v9): its label in a column of <see cref="LabelWidth"/> and the control with its hints to the
/// right of it; in the narrow window (<see cref="Narrow"/>, set by the theme under <c>.narrow</c>) the label above the
/// control. The first child is the label, the others are stacked under each other in the control's column.
/// </summary>
public sealed class FieldPanel : Panel
{
    public static readonly StyledProperty<bool> NarrowProperty = AvaloniaProperty.Register<FieldPanel, bool>(nameof(Narrow));
    public static readonly StyledProperty<double> LabelWidthProperty = AvaloniaProperty.Register<FieldPanel, double>(nameof(LabelWidth), 200);
    public static readonly StyledProperty<double> GapProperty = AvaloniaProperty.Register<FieldPanel, double>(nameof(Gap), 16);
    public static readonly StyledProperty<double> NarrowGapProperty = AvaloniaProperty.Register<FieldPanel, double>(nameof(NarrowGap), 6);

    /// <summary>The label at the top of its row (a list beside it), not centred on the first control.</summary>
    public static readonly StyledProperty<bool> LabelTopProperty = AvaloniaProperty.Register<FieldPanel, bool>(nameof(LabelTop));

    static FieldPanel() => AffectsMeasure<FieldPanel>(NarrowProperty, LabelWidthProperty, GapProperty, NarrowGapProperty, LabelTopProperty);

    public bool Narrow { get => GetValue(NarrowProperty); set => SetValue(NarrowProperty, value); }
    public double LabelWidth { get => GetValue(LabelWidthProperty); set => SetValue(LabelWidthProperty, value); }
    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }
    public double NarrowGap { get => GetValue(NarrowGapProperty); set => SetValue(NarrowGapProperty, value); }
    public bool LabelTop { get => GetValue(LabelTopProperty); set => SetValue(LabelTopProperty, value); }

    private IEnumerable<Control> Content => Children.Skip(1).Where(c => c.IsVisible);

    protected override Size MeasureOverride(Size available)
    {
        if (Children.Count == 0) return default;
        var label = Children[0];
        if (Narrow)
        {
            label.Measure(new Size(available.Width, double.PositiveInfinity));
            var height = label.IsVisible ? label.DesiredSize.Height : 0;
            var width = label.DesiredSize.Width;
            var first = true;
            foreach (var child in Content)
            {
                child.Measure(new Size(available.Width, double.PositiveInfinity));
                height += (first && label.IsVisible ? NarrowGap : 0) + child.DesiredSize.Height;
                width = Math.Max(width, child.DesiredSize.Width);
                first = false;
            }
            return new Size(double.IsInfinity(available.Width) ? width : available.Width, height);
        }
        var right = Math.Max(0, available.Width - LabelWidth - Gap);
        label.Measure(new Size(LabelWidth, double.PositiveInfinity));
        var stack = 0.0;
        foreach (var child in Content)
        {
            child.Measure(new Size(right, double.PositiveInfinity));
            stack += child.DesiredSize.Height;
        }
        var total = Math.Max(label.DesiredSize.Height, stack);
        return new Size(double.IsInfinity(available.Width) ? LabelWidth + Gap + Content.Select(c => c.DesiredSize.Width).DefaultIfEmpty(0).Max() : available.Width, total);
    }

    protected override Size ArrangeOverride(Size final)
    {
        if (Children.Count == 0) return final;
        var label = Children[0];
        if (Narrow)
        {
            var y = 0.0;
            if (label.IsVisible)
            {
                label.Arrange(new Rect(0, 0, final.Width, label.DesiredSize.Height));
                y = label.DesiredSize.Height + NarrowGap;
            }
            foreach (var child in Content)
            {
                child.Arrange(new Rect(0, y, final.Width, child.DesiredSize.Height));
                y += child.DesiredSize.Height;
            }
            return final;
        }
        var x = LabelWidth + Gap;
        var width = Math.Max(0, final.Width - x);
        var top = 0.0;
        var firstHeight = 0.0;
        foreach (var child in Content)
        {
            if (firstHeight == 0) firstHeight = child.DesiredSize.Height;
            child.Arrange(new Rect(x, top, width, child.DesiredSize.Height));
            top += child.DesiredSize.Height;
        }
        // Centred on the first control's line (a long label just starts at the top).
        var labelTop = LabelTop || label.DesiredSize.Height >= firstHeight ? 0 : (firstHeight - label.DesiredSize.Height) / 2;
        label.Arrange(new Rect(0, labelTop, LabelWidth, label.DesiredSize.Height));
        return final;
    }
}

/// <summary>
/// Two blocks side by side, the second <see cref="Ratio"/> times as wide, <see cref="Gap"/> between (the trophies' header);
/// in the narrow window one under the other, <see cref="NarrowGap"/> between.
/// </summary>
public sealed class SplitPanel : Panel
{
    public static readonly StyledProperty<bool> NarrowProperty = AvaloniaProperty.Register<SplitPanel, bool>(nameof(Narrow));
    public static readonly StyledProperty<double> RatioProperty = AvaloniaProperty.Register<SplitPanel, double>(nameof(Ratio), 1.15);
    public static readonly StyledProperty<double> GapProperty = AvaloniaProperty.Register<SplitPanel, double>(nameof(Gap), 24);
    public static readonly StyledProperty<double> NarrowGapProperty = AvaloniaProperty.Register<SplitPanel, double>(nameof(NarrowGap), 12);

    static SplitPanel() => AffectsMeasure<SplitPanel>(NarrowProperty, RatioProperty, GapProperty, NarrowGapProperty);

    public bool Narrow { get => GetValue(NarrowProperty); set => SetValue(NarrowProperty, value); }
    public double Ratio { get => GetValue(RatioProperty); set => SetValue(RatioProperty, value); }
    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }
    public double NarrowGap { get => GetValue(NarrowGapProperty); set => SetValue(NarrowGapProperty, value); }

    private (double First, double Second) Widths(double width)
    {
        var first = Math.Max(0, (width - Gap) / (1 + Ratio));
        return (first, Math.Max(0, width - Gap - first));
    }

    protected override Size MeasureOverride(Size available)
    {
        if (Children.Count < 2) return default;
        if (Narrow)
        {
            Children[0].Measure(new Size(available.Width, double.PositiveInfinity));
            Children[1].Measure(new Size(available.Width, double.PositiveInfinity));
            return new Size(available.Width, Children[0].DesiredSize.Height + NarrowGap + Children[1].DesiredSize.Height);
        }
        var (a, b) = Widths(available.Width);
        Children[0].Measure(new Size(a, double.PositiveInfinity));
        Children[1].Measure(new Size(b, double.PositiveInfinity));
        return new Size(available.Width, Math.Max(Children[0].DesiredSize.Height, Children[1].DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size final)
    {
        if (Children.Count < 2) return final;
        if (Narrow)
        {
            Children[0].Arrange(new Rect(0, 0, final.Width, Children[0].DesiredSize.Height));
            Children[1].Arrange(new Rect(0, Children[0].DesiredSize.Height + NarrowGap, final.Width, Children[1].DesiredSize.Height));
            return final;
        }
        var (a, b) = Widths(final.Width);
        Children[0].Arrange(new Rect(0, (final.Height - Children[0].DesiredSize.Height) / 2, a, Children[0].DesiredSize.Height));
        Children[1].Arrange(new Rect(a + Gap, (final.Height - Children[1].DesiredSize.Height) / 2, b, Children[1].DesiredSize.Height));
        return final;
    }
}

/// <summary>
/// A trophy's icon: its own picture from the game copy with a small rank badge in the corner, or (no picture) the rank
/// badge alone. <see cref="Dim"/>: not earned yet (the picture faded).
/// </summary>
public sealed class TrophyIcon : Control
{
    public static readonly StyledProperty<IImage?> PictureProperty = AvaloniaProperty.Register<TrophyIcon, IImage?>(nameof(Picture));
    public static readonly StyledProperty<IImage?> BadgeProperty = AvaloniaProperty.Register<TrophyIcon, IImage?>(nameof(Badge));
    public static readonly StyledProperty<bool> DimProperty = AvaloniaProperty.Register<TrophyIcon, bool>(nameof(Dim));

    static TrophyIcon() => AffectsRender<TrophyIcon>(PictureProperty, BadgeProperty, DimProperty);

    public IImage? Picture { get => GetValue(PictureProperty); set => SetValue(PictureProperty, value); }
    public IImage? Badge { get => GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }
    public bool Dim { get => GetValue(DimProperty); set => SetValue(DimProperty, value); }

    public override void Render(DrawingContext context) => Paint(context, new Rect(Bounds.Size), Picture, Badge, Dim);

    private static readonly IPen Frame = new Pen(new SolidColorBrush(Color.Parse("#8A7550")), 1);
    private static readonly IPen FrameDim = new Pen(new SolidColorBrush(Color.Parse("#2E262C")), 1);

    /// <summary>The icon into a square (also the in-game notices' picture, <see cref="Services.TrophyToastRenderer"/>).</summary>
    public static void Paint(DrawingContext context, Rect box, IImage? picture, IImage? badge, bool dim)
    {
        var size = Math.Min(box.Width, box.Height);
        var square = new Rect(box.X + (box.Width - size) / 2, box.Y + (box.Height - size) / 2, size, size);
        if (picture is null)
        {
            if (badge is not null) Place(context, badge, square);
            return;
        }
        // The picture inset a little so the corner badge can overhang it; a hairline frame (gold earned, stone not).
        var corner = Math.Round(size * 0.44);
        var art = new Rect(square.X, square.Y, size - corner * 0.18, size - corner * 0.18);
        using (context.PushOpacity(dim ? 0.38 : 1))
            Place(context, picture, art);
        var hair = Math.Max(1, size / 64);
        context.DrawRectangle(null, dim ? FrameDim : Frame, art.Deflate(hair / 2));
        if (badge is not null) Place(context, badge, new Rect(square.Right - corner, square.Bottom - corner, corner, corner));
    }

    /// <summary>
    /// An image into a rectangle: moved there first, drawn at the origin (a DrawingImage scales its destination's offset
    /// with its drawing, so it is only right at 0,0).
    /// </summary>
    private static void Place(DrawingContext context, IImage image, Rect dest)
    {
        using (context.PushTransform(Matrix.CreateTranslation(dest.X, dest.Y)))
            image.Draw(context, new Rect(image.Size), new Rect(dest.Size));
    }
}

/// <summary>
/// A thin progress line (the trophies' header and widget): the track, the part done (0–100 of <see cref="Value"/>)
/// filled over it. A plain drawing, not a ProgressBar: nothing to animate, no template.
/// </summary>
public sealed class Meter : Control
{
    public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<Meter, double>(nameof(Value));
    public static readonly StyledProperty<IBrush?> TrackProperty = AvaloniaProperty.Register<Meter, IBrush?>(nameof(Track));
    public static readonly StyledProperty<IBrush?> FillProperty = AvaloniaProperty.Register<Meter, IBrush?>(nameof(Fill));
    public static readonly StyledProperty<IBrush?> HeadProperty = AvaloniaProperty.Register<Meter, IBrush?>(nameof(Head));

    static Meter() => AffectsRender<Meter>(ValueProperty, TrackProperty, FillProperty, HeadProperty);

    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public IBrush? Track { get => GetValue(TrackProperty); set => SetValue(TrackProperty, value); }
    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    /// <summary>A mark at the end of the part done (the header's light gold head); none when null.</summary>
    public IBrush? Head { get => GetValue(HeadProperty); set => SetValue(HeadProperty, value); }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (Track is not null) context.FillRectangle(Track, new Rect(size));
        var done = Math.Round(size.Width * Math.Clamp(Value, 0, 100) / 100);
        if (done <= 0) return;
        if (Fill is not null) context.FillRectangle(Fill, new Rect(0, 0, done, size.Height));
        if (Head is not null) context.FillRectangle(Head, new Rect(Math.Max(0, done - 1), -1, 2, size.Height + 2));
    }
}
