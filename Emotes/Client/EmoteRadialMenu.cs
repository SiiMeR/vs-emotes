using System;
using System.Collections.Generic;
using System.Linq;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Emotes;

public record EmoteMenuEntry(string Code, string Label, bool Favorite);

public class EmoteRadialMenu : IDisposable
{
    private const double CairoUpOffset = 3d * Math.PI / 2d;
    private const float DeadZone = 0.9f;
    private const int Gap = 5;
    private const int Padding = 20;
    private const int LabelGap = 14;
    private const float DrawZ = 9000f;
    private const double LineWidth = 6d;
    private const int RingBand = (int)LineWidth;
    private const int PatternAlpha = 64;
    private const float PatternScale = 0.125f;
    private const double TextCover = 0.85d;
    private const double MinFontSize = 10d;
    private const int BoxSamples = 17;
    private const double StarSize = 16d;
    private const double StarGap = 2d;
    private const double MinChord = 115d;
    private const double MaxScreenShare = 0.42d;

    private static readonly double[] SliceIdle = { 0.24d, 0.22d, 0.20d };
    private static readonly double[] SliceHover = { 0.36d, 0.33d, 0.30d };
    private static readonly double[] HoverOutline = GuiStyle.ColorTime1;
    private static readonly double[] StarColor = GuiStyle.ColorTime1;
    private static readonly AssetLocation StarIcon = new("game", "textures/icons/worldmap/star2.svg");

    private class Slice
    {
        public string Code;
        public LoadedTexture Idle;
        public LoadedTexture Hover;
        public int OffsetX;
        public int OffsetY;
        public bool Favorite;
        public float StarX;
        public float StarY;
    }

    private readonly ICoreClientAPI capi;
    private readonly int innerUnscaled;
    private readonly int outerUnscaled;
    private readonly List<Slice> slices = new();
    private readonly TextBackground labelBackground = new()
    {
        FillColor = GuiStyle.DialogStrongBgColor,
        BorderColor = GuiStyle.DialogBorderColor,
        BorderWidth = 2d,
        Padding = 8,
        Radius = 4d
    };

    private LoadedTexture star;
    private LoadedTexture centre;
    private LoadedTexture categoryLabel;
    private LoadedTexture emptyLabel;
    private LoadedTexture hint;
    private int innerRadius;
    private int outerRadius;
    private int midX;
    private int midY;
    private int selected = -1;
    private bool disposed;

    public bool Opened { get; private set; }

    public string HoveredCode => selected >= 0 && selected < slices.Count ? slices[selected].Code : null;

    public EmoteRadialMenu(ICoreClientAPI capi, int innerRadius, int outerRadius)
    {
        this.capi = capi;
        innerUnscaled = Math.Max(16, innerRadius);
        outerUnscaled = Math.Max(innerUnscaled + 16, outerRadius);
    }

    public void Build(IReadOnlyList<EmoteMenuEntry> entries, string centreText, string emptyText)
    {
        DisposeSlices();
        emptyLabel?.Dispose();
        emptyLabel = null;
        if (disposed) return;

        UpdateScreen();
        var count = entries?.Count ?? 0;
        var growth = Growth(count);
        innerRadius = (int)(GuiElement.scaled(innerUnscaled) * growth);
        outerRadius = (int)(GuiElement.scaled(outerUnscaled) * growth);
        BakeCentre();
        BakeCategoryLabel(centreText ?? "");
        hint ??= capi.Gui.TextTexture.GenUnscaledTextTexture(Lang.Get("emotes:wheel-hint"),
            CairoFont.WhiteDetailText(), labelBackground);
        star?.Dispose();
        var starSize = (int)GuiElement.scaled(StarSize);
        star = capi.Gui.LoadSvg(StarIcon, starSize, starSize, starSize, starSize, Argb(StarColor));

        if (entries == null || entries.Count == 0)
        {
            emptyLabel = capi.Gui.TextTexture.GenUnscaledTextTexture(emptyText ?? "", CairoFont.WhiteSmallText(),
                labelBackground);
            return;
        }

        var mid = (innerRadius + outerRadius) / 2;
        var thickness = outerRadius - innerRadius;
        var step = GameMath.TWOPI / entries.Count;
        var chord = (int)(2f * mid * MathF.Sin(step / 2f));

        foreach (var entry in entries)
        {
            var angle = slices.Count * step;
            var box = SliceBox(mid, thickness, angle, step);
            var slice = new Slice
            {
                Code = entry.Code,
                OffsetX = box.X,
                OffsetY = box.Y,
                Favorite = entry.Favorite,
                Idle = BakeSlice(box, mid, thickness, chord, angle, step, false, entry, out var starPos),
                StarX = (float)starPos.X,
                StarY = (float)starPos.Y
            };
            slice.Hover = BakeSlice(box, mid, thickness, chord, angle, step, true, entry, out _);
            slices.Add(slice);
        }
    }

    private double Growth(int count)
    {
        if (count < 2) return 1d;

        var baseInner = GuiElement.scaled(innerUnscaled);
        var baseOuter = GuiElement.scaled(outerUnscaled);
        var baseMid = (baseInner + baseOuter) / 2d;
        var requiredMid = GuiElement.scaled(MinChord) / (2d * Math.Sin(Math.PI / count));
        var maxGrowth = Math.Max(1d, capi.Render.FrameHeight * MaxScreenShare / baseOuter);
        return Math.Clamp(requiredMid / baseMid, 1d, maxGrowth);
    }

    private static (int X, int Y, int W, int H) SliceBox(int mid, int thickness, float angle, float step)
    {
        double outer = mid + thickness / 2d;
        double inner = mid - thickness / 2d;
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;

        for (var i = 0; i < BoxSamples; i++)
        {
            var a = angle - step / 2d + step * i / (BoxSamples - 1);
            foreach (var r in new[] { inner, outer })
            {
                var x = r * Math.Sin(a);
                var y = -r * Math.Cos(a);
                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }
        }

        var x0 = (int)Math.Floor(minX - LineWidth);
        var y0 = (int)Math.Floor(minY - LineWidth);
        return (x0, y0, (int)Math.Ceiling(maxX + LineWidth) - x0, (int)Math.Ceiling(maxY + LineWidth) - y0);
    }

    private LoadedTexture BakeSlice((int X, int Y, int W, int H) box, int mid, int thickness, int chord, float angle,
        float step, bool hover, EmoteMenuEntry entry, out (double X, double Y) starPos)
    {
        var surface = new ImageSurface(Format.Argb32, Math.Max(1, box.W), Math.Max(1, box.H));
        var ctx = new Context(surface);
        ctx.Translate(-box.X, -box.Y);

        var fill = hover ? SliceHover : SliceIdle;
        PushSlicePath(ctx, mid, thickness, angle, step);
        ctx.SetSourceRGBA(fill[0], fill[1], fill[2], 1d);
        ctx.FillPreserve();
        ctx.SetSource(GuiElement.getPattern(capi, GuiElement.dirtTextureName, true, PatternAlpha, PatternScale));
        ctx.Fill();

        ctx.Save();
        PushSlicePath(ctx, mid, thickness, angle, step);
        ctx.Clip();
        starPos = PaintLabel(ctx, mid * Math.Sin(angle), -mid * Math.Cos(angle), chord * TextCover,
            thickness * TextCover, hover, entry);
        ctx.Restore();

        PushSlicePath(ctx, mid, thickness, angle, step);
        ctx.LineWidth = LineWidth;
        var outline = hover ? HoverOutline : GuiStyle.DialogBorderColor;
        ctx.SetSourceRGBA(outline[0], outline[1], outline[2], outline.Length > 3 ? outline[3] : 1d);
        ctx.Stroke();

        var texture = new LoadedTexture(capi);
        capi.Gui.LoadOrUpdateCairoTexture(surface, true, ref texture);
        ctx.Dispose();
        surface.Dispose();
        return texture;
    }

    private (double X, double Y) PaintLabel(Context ctx, double cx, double cy, double boxW, double boxH, bool hover,
        EmoteMenuEntry entry)
    {
        var label = entry.Label;
        var starSize = entry.Favorite ? GuiElement.scaled(StarSize) : 0d;
        var starH = entry.Favorite ? starSize + StarGap : 0d;
        if (string.IsNullOrEmpty(label) || boxW < 1 || boxH < 1) return (cx - starSize / 2d, cy - starSize / 2d);

        var font = CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold).WithOrientation(EnumTextOrientation.Center);
        if (hover) font.Color = (double[])GuiStyle.ActiveButtonTextColor.Clone();
        FitFont(font, label, boxW, boxH - starH);

        var textH = capi.Gui.Text.GetMultilineTextHeight(font, label, boxW);
        var top = cy - (textH + starH) / 2d;
        font.SetupContext(ctx);
        capi.Gui.Text.AutobreakAndDrawMultilineTextAt(ctx, font, label, cx - boxW / 2d, top + starH, boxW,
            EnumTextOrientation.Center);
        return (cx - starSize / 2d, top);
    }

    private static int Argb(double[] color)
    {
        return (255 << 24) | ((int)(color[0] * 255d) << 16) | ((int)(color[1] * 255d) << 8) | (int)(color[2] * 255d);
    }

    private void FitFont(CairoFont font, string text, double boxW, double boxH)
    {
        while (font.UnscaledFontsize > MinFontSize && !Fits(font, text, boxW, boxH)) font.UnscaledFontsize -= 1d;
    }

    private bool Fits(CairoFont font, string text, double boxW, double boxH)
    {
        var widest = text.Split(' ').Max(word => font.GetTextExtents(word).Width);
        return widest <= boxW && capi.Gui.Text.GetMultilineTextHeight(font, text, boxW) <= boxH;
    }

    private static void PushSlicePath(Context ctx, int mid, int thickness, float angle, float step)
    {
        var half = step / 2f;
        var outer = mid + thickness / 2f;
        var inner = mid - thickness / 2f;
        var outerGap = Gap / outer;
        var innerGap = Gap / inner;
        var innerEnd = angle + half - innerGap;

        ctx.NewPath();
        ctx.Arc(0d, 0d, outer, angle - half + outerGap + CairoUpOffset, angle + half - outerGap + CairoUpOffset);
        ctx.LineTo(inner * Math.Sin(innerEnd), -inner * Math.Cos(innerEnd));
        ctx.ArcNegative(0d, 0d, inner, angle + half - innerGap + CairoUpOffset, angle - half + innerGap + CairoUpOffset);
        ctx.ClosePath();
    }

    private void BakeCentre()
    {
        var outer = Math.Max(8, innerRadius - (int)(LineWidth / 2d) - Gap);
        var inner = Math.Max(2, outer - RingBand);
        var size = outer * 2 + Padding;
        var surface = new ImageSurface(Format.Argb32, size, size);
        var ctx = new Context(surface);
        var color = GuiStyle.DarkBrownColor;
        var c = size / 2d;

        ctx.Arc(c, c, outer, 0d, GameMath.TWOPI);
        ctx.ClosePath();
        ctx.ArcNegative(c, c, inner, GameMath.TWOPI, 0d);
        ctx.ClosePath();

        ctx.SetSourceRGBA(color[0], color[1], color[2], 1d);
        ctx.Fill();

        centre ??= new LoadedTexture(capi);
        capi.Gui.LoadOrUpdateCairoTexture(surface, true, ref centre);
        ctx.Dispose();
        surface.Dispose();
    }

    private void BakeCategoryLabel(string text)
    {
        categoryLabel?.Dispose();
        categoryLabel = null;
        if (text.Length == 0) return;

        var font = CairoFont.WhiteMediumText().WithWeight(FontWeight.Bold);
        categoryLabel = capi.Gui.TextTexture.GenUnscaledTextTexture(text, font, labelBackground);
    }

    public void Open()
    {
        if (disposed) return;

        UpdateScreen();
        selected = -1;
        Opened = true;
    }

    public string Close()
    {
        Opened = false;
        var code = HoveredCode;
        selected = -1;
        return code;
    }

    public void Point(int mouseX, int mouseY)
    {
        if (!Opened || slices.Count == 0) return;

        float dx = mouseX - midX;
        float dy = mouseY - midY;
        if (MathF.Sqrt(dx * dx + dy * dy) < innerRadius * DeadZone)
        {
            selected = -1;
            return;
        }

        var step = GameMath.TWOPI / slices.Count;
        var theta = MathF.Atan2(dx, -dy);
        if (theta < 0f) theta += GameMath.TWOPI;

        selected = (int)MathF.Round(theta / step) % slices.Count;
    }

    public void Render()
    {
        if (!Opened) return;

        for (var i = 0; i < slices.Count; i++)
        {
            var slice = slices[i];
            DrawAt(i == selected ? slice.Hover : slice.Idle, midX + slice.OffsetX, midY + slice.OffsetY);
        }

        foreach (var slice in slices)
        {
            if (slice.Favorite) DrawAt(star, midX + slice.StarX, midY + slice.StarY);
        }

        DrawCentred(centre);
        DrawAbove(categoryLabel, outerRadius);
        if (slices.Count == 0) DrawBelow(emptyLabel, innerRadius);
        DrawBelow(hint, outerRadius);
    }

    private void DrawAbove(LoadedTexture texture, int radius)
    {
        if (texture == null || texture.Disposed) return;
        DrawAt(texture, midX - texture.Width / 2f, midY - radius - LabelGap - texture.Height);
    }

    private void DrawCentred(LoadedTexture texture)
    {
        if (texture == null || texture.Disposed) return;
        DrawAt(texture, midX - texture.Width / 2f, midY - texture.Height / 2f);
    }

    private void DrawBelow(LoadedTexture texture, int radius)
    {
        if (texture == null || texture.Disposed) return;
        DrawAt(texture, midX - texture.Width / 2f, midY + radius + LabelGap);
    }

    private void DrawAt(LoadedTexture texture, float x, float y)
    {
        if (texture == null || texture.Disposed) return;
        capi.Render.Render2DLoadedTexture(texture, x, y, DrawZ);
    }

    private void UpdateScreen()
    {
        midX = capi.Render.FrameWidth / 2;
        midY = capi.Render.FrameHeight / 2;
    }

    private void DisposeSlices()
    {
        foreach (var slice in slices)
        {
            slice.Idle?.Dispose();
            slice.Hover?.Dispose();
        }

        slices.Clear();
    }

    public void Dispose()
    {
        disposed = true;
        Opened = false;
        DisposeSlices();
        star?.Dispose();
        star = null;
        centre?.Dispose();
        centre = null;
        categoryLabel?.Dispose();
        categoryLabel = null;
        emptyLabel?.Dispose();
        emptyLabel = null;
        hint?.Dispose();
        hint = null;
    }
}
