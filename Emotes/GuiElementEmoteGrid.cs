using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Cairo;
using Vintagestory.API.Client;

namespace Emotes;

public class GuiElementEmoteGrid : GuiElement
{
    public const double CellW = 115;
    public const double CellH = 44;
    public const double CellPad = 3;

    public Action<int> OnSlotClick;
    public int SelectedIndex = -1;

    readonly List<string> names;
    readonly int cols;
    readonly int rows;
    LoadedTexture hoverTexture;

    static readonly Regex CamelSplit = new(@"(?<=[a-z\d])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])");

    public GuiElementEmoteGrid(ICoreClientAPI capi, List<string> names, int cols, Action<int> onSlotClick, ElementBounds bounds)
        : base(capi, bounds)
    {
        this.names = names;
        this.cols = cols;
        rows = (names.Count + cols - 1) / cols;
        OnSlotClick = onSlotClick;
        hoverTexture = new LoadedTexture(capi);

        Bounds.fixedWidth = cols * (CellW + CellPad);
        Bounds.fixedHeight = rows * (CellH + CellPad);
    }

    public override void ComposeElements(Context ctx, ImageSurface surface)
    {
        Bounds.CalcWorldBounds();
        double cw = scaled(CellW), ch = scaled(CellH), cp = scaled(CellPad);

        var font = CairoFont.WhiteSmallText().WithFontSize(11);
        font.SetupContext(ctx);

        for (int i = 0; i < names.Count; i++)
        {
            int r = i / cols, c = i % cols;
            double x = Bounds.drawX + c * (cw + cp);
            double y = Bounds.drawY + r * (ch + cp);

            ctx.SetSourceRGBA(0, 0, 0, 0.3);
            RoundRectangle(ctx, x, y, cw, ch, GuiStyle.ElementBGRadius);
            ctx.Fill();
            EmbossRoundRectangleElement(ctx, x, y, cw, ch, inverse: false);

            font.SetupContext(ctx);
            DrawNameCentered(ctx, names[i], x, y, cw, ch);
        }

        ComposeHoverTexture((int)cw, (int)ch);
    }

    void DrawNameCentered(Context ctx, string name, double x, double y, double cw, double ch)
    {
        var lines = SplitIntoLines(ctx, name, cw - 8);
        FontExtents fe = ctx.FontExtents;
        double lineH = fe.Height;
        double totalH = lineH * lines.Count;
        double startY = y + (ch - totalH) / 2 + fe.Ascent;

        foreach (var line in lines)
        {
            TextExtents te = ctx.TextExtents(line);
            ctx.MoveTo(x + (cw - te.Width) / 2, startY);
            ctx.ShowText(line);
            startY += lineH;
        }
    }

    List<string> SplitIntoLines(Context ctx, string name, double maxWidth)
    {
        string[] words = CamelSplit.Split(name);
        var lines = new List<string>();
        string current = "";
        foreach (var word in words)
        {
            string candidate = current.Length == 0 ? word : current + " " + word;
            if (ctx.TextExtents(candidate).Width > maxWidth && current.Length > 0)
            {
                lines.Add(current);
                current = word;
            }
            else current = candidate;
        }
        if (current.Length > 0) lines.Add(current);
        return lines;
    }

    void ComposeHoverTexture(int cw, int ch)
    {
        var s = new ImageSurface(Format.Argb32, cw, ch);
        var c = genContext(s);
        c.SetSourceRGBA(1, 1, 1, 0.15);
        RoundRectangle(c, 1, 1, cw - 2, ch - 2, GuiStyle.ElementBGRadius);
        c.Fill();
        generateTexture(s, ref hoverTexture);
        c.Dispose();
        s.Dispose();
    }

    public override void RenderInteractiveElements(float deltaTime)
    {
        double cw = scaled(CellW), ch = scaled(CellH), cp = scaled(CellPad);
        int mx = api.Input.MouseX - (int)Bounds.absX;
        int my = api.Input.MouseY - (int)Bounds.absY;

        for (int i = 0; i < names.Count; i++)
        {
            int r = i / cols, c = i % cols;
            double x = c * (cw + cp), y = r * (ch + cp);
            bool hovered = mx >= x && my >= y && mx < x + cw + cp && my < y + ch + cp;

            if (hovered || i == SelectedIndex)
                api.Render.Render2DTexture(hoverTexture.TextureId,
                    (float)(Bounds.renderX + x), (float)(Bounds.renderY + y), (float)cw, (float)ch);
        }
    }

    public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
    {
        base.OnMouseDownOnElement(api, args);
        double cw = scaled(CellW), ch = scaled(CellH), cp = scaled(CellPad);
        int mx = api.Input.MouseX - (int)Bounds.absX;
        int my = api.Input.MouseY - (int)Bounds.absY;
        int c = (int)(mx / (cw + cp));
        int r = (int)(my / (ch + cp));
        int idx = r * cols + c;
        if (idx >= 0 && idx < names.Count) OnSlotClick?.Invoke(idx);
    }

    public override bool Focusable => true;

    public override void Dispose()
    {
        base.Dispose();
        hoverTexture?.Dispose();
    }
}
