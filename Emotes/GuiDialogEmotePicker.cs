using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace Emotes;

public class GuiDialogEmotePicker : GuiDialog
{
    public override string ToggleKeyCombinationCode => "emotepicker";

    const double Pad = 10;
    const int Cols = 2;

    List<string> emoteCodes;
    GuiElementEmoteGrid emoteGrid;
    EmotesModSystem modSystem;

    public GuiDialogEmotePicker(ICoreClientAPI capi, EmotesModSystem modSystem) : base(capi)
    {
        this.modSystem = modSystem;
    }

    void ComposeDialog()
    {
        var emotes = EmotesModSystem.GetEmotes();
        emoteCodes = emotes.Keys.ToList();
        var names = emotes.Values.Select(e => e.Name).ToList();

        int rows = (names.Count + Cols - 1) / Cols;
        double gridH = rows * (GuiElementEmoteGrid.CellH + GuiElementEmoteGrid.CellPad);
        double contentW = Cols * (GuiElementEmoteGrid.CellW + GuiElementEmoteGrid.CellPad);

        var gridBounds = ElementBounds.Fixed(0, GuiStyle.TitleBarHeight, contentW, gridH);
        var contentBounds = ElementBounds.Fill.WithFixedPadding(Pad);
        contentBounds.BothSizing = ElementSizing.FitToChildren;

        var dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.RightMiddle)
            .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);

        emoteGrid = new GuiElementEmoteGrid(capi, names, Cols, OnSlotClick, gridBounds);

        var composer = capi.Gui
            .CreateCompo("emotepicker", dialogBounds)
            .AddShadedDialogBG(contentBounds, withTitleBar: true)
            .AddDialogTitleBar("Emotes", () => TryClose())
            .BeginChildElements(contentBounds);

        composer.AddInteractiveElement(emoteGrid, "emotegrid");

        composer.EndChildElements().Compose();

        SingleComposer = composer;
    }

    void OnSlotClick(int index)
    {
        if (index < 0 || index >= emoteCodes.Count) return;
        modSystem.SendToggleEmote(emoteCodes[index]);
        TryClose();
    }

    public override void OnGuiOpened()
    {
        ComposeDialog();
    }

    public override bool PrefersUngrabbedMouse => false;
    public override bool ShouldReceiveKeyboardEvents() => false;
}
