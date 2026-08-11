using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace Emotes;

public class GuiDialogEmotePicker : GuiDialog
{
    private const double Pad = 10;
    private const double MinTabW = 100;
    private const double MaxTabW = 220;
    private const double TabH = 32;
    private const double BtnW = 195;
    private const double BtnH = 36;
    private const double BtnPad = 3;
    private const double SearchH = 30;
    private const double DropH = 30;
    private const double AllListH = 400;
    private const int Cols = 2;

    private const string AllKey = "all";
    private const string VanillaKey = "vanilla";
    private const string MiscKey = "misc";

    private static readonly string[] VanillaEmoteCodes =
        { "wave", "cheer", "shrug", "cry", "nod", "facepalm", "bow", "laugh", "rage" };

    private readonly EmotesModSystem modSystem;
    private string activeKey = AllKey;
    private (string Key, string Label)[] tabs = Array.Empty<(string, string)>();
    private bool useDropDown;
    private double tabWidth = MinTabW;
    private Entity cachedEntitySelection;
    private string searchText = "";
    private bool searchPending;
    private long openedMs;
    private ElementBounds allListBounds;

    public GuiDialogEmotePicker(ICoreClientAPI capi, EmotesModSystem modSystem) : base(capi)
    {
        this.modSystem = modSystem;
    }

    public override string ToggleKeyCombinationCode => "emotepicker";
    public override bool PrefersUngrabbedMouse => capi.Settings.Bool["immersiveMouseMode"];
    public override bool ShouldReceiveKeyboardEvents() => IsOpened() && activeKey == AllKey;

    public override void OnKeyDown(KeyEvent args)
    {
        var hotkey = capi.Input.GetHotKeyByCode(ToggleKeyCombinationCode);
        if (hotkey != null && hotkey.DidPress(args, capi.World, capi.World.Player, true) && TryClose())
        {
            ignoreNextKeyPress = true;
            args.Handled = true;
            return;
        }

        base.OnKeyDown(args);
    }

    private string[] GetVanillaEmoteCodes()
    {
        var attr = capi.World.Player?.Entity?.Properties?.Attributes?["emotes"];
        var codes = attr?.AsArray<string>()?.Where(c => !string.IsNullOrEmpty(c)).ToArray();
        return codes is { Length: > 0 } ? codes : VanillaEmoteCodes;
    }

    private void BuildTabs()
    {
        var categories = modSystem.Emotes.Values
            .Where(e => !modSystem.IsEmoteDisabled(e.Code))
            .GroupBy(e => string.IsNullOrEmpty(e.Category) ? MiscKey : e.Category)
            .Select(g => (Key: g.Key, Label: modSystem.GetCategoryName(g.Key), Order: g.Min(e => e.CategoryOrder)))
            .OrderBy(c => c.Key == MiscKey ? 1 : 0)
            .ThenBy(c => c.Order)
            .ThenBy(c => c.Label, StringComparer.CurrentCultureIgnoreCase);

        var list = new List<(string Key, string Label)> { (AllKey, Lang.Get("emotes:cat-all")) };
        list.AddRange(categories.Select(c => (c.Key, c.Label)));
        list.Add((VanillaKey, Lang.Get("emotes:cat-vanilla")));
        tabs = list.ToArray();

        if (tabs.All(t => t.Key != activeKey)) activeKey = AllKey;

        var font = CairoFont.WhiteSmallText();
        double widest = 0;
        foreach (var tab in tabs) widest = Math.Max(widest, font.GetTextExtents(tab.Label).Width);
        tabWidth = Math.Min(MaxTabW, Math.Max(MinTabW, widest / RuntimeEnv.GUIScale + 8));

        var available = capi.Render.FrameHeight / RuntimeEnv.GUIScale
                        - 2 * GuiStyle.DialogToScreenPadding - GuiStyle.TitleBarHeight;
        useDropDown = tabs.Length > Math.Max(3, (int)(available / TabH));
    }

    private int ActiveIndex()
    {
        for (var i = 0; i < tabs.Length; i++)
            if (tabs[i].Key == activeKey)
                return i;
        return 0;
    }

    private GuiComposer AddSelector(GuiComposer composer, ElementBounds dropBounds)
    {
        if (useDropDown)
        {
            return composer.AddDropDown(tabs.Select(t => t.Key).ToArray(), tabs.Select(t => t.Label).ToArray(),
                ActiveIndex(), OnCategorySelected, dropBounds, "categories");
        }

        var guiTabs = tabs.Select((t, i) => new GuiTab { Name = t.Label, DataInt = i }).ToArray();
        var tabBounds = ElementBounds.Fixed(-(Pad + tabWidth), GuiStyle.TitleBarHeight, tabWidth, tabs.Length * TabH);
        return composer.AddVerticalTabs(guiTabs, tabBounds, (idx, _) =>
        {
            if (idx < 0 || idx >= tabs.Length) return;
            activeKey = tabs[idx].Key;
            ComposeDialog();
        }, "tabs");
    }

    private void OnCategorySelected(string code, bool selected)
    {
        if (!selected || code == activeKey) return;
        activeKey = code;
        capi.Event.EnqueueMainThreadTask(ComposeDialog, "emotes-category");
    }

    private void FinishSelector(GuiComposer composer)
    {
        if (useDropDown) return;

        var tabsElement = composer.GetVerticalTab("tabs");
        if (tabsElement == null) return;
        tabsElement.SetValue(ActiveIndex(), false);
    }

    private void ComposeDialog()
    {
        BuildTabs();

        if (activeKey == AllKey)
        {
            ComposeAllTab();
            return;
        }

        var isVanilla = activeKey == VanillaKey;

        string[] codes, names;
        if (isVanilla)
        {
            codes = GetVanillaEmoteCodes();
            names = codes.Select(modSystem.GetEmoteName).ToArray();
        }
        else
        {
            var emotes = modSystem.Emotes.Values
                .Where(e => e.Category == activeKey && !modSystem.IsEmoteDisabled(e.Code))
                .Select(e => (e.Code, Name: modSystem.GetEmoteName(e)))
                .OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
            codes = emotes.Select(e => e.Code).ToArray();
            names = emotes.Select(e => e.Name).ToArray();
        }

        var rows = Math.Max(1, (names.Length + Cols - 1) / Cols);
        var contentW = Cols * (BtnW + BtnPad);
        var topY = GuiStyle.TitleBarHeight + Pad;
        var dropBounds = ElementBounds.Fixed(0, topY, contentW, DropH);
        var gridStartY = useDropDown ? topY + DropH + 5 : topY;

        var contentBounds = ElementBounds.Fixed(0, 0, contentW, gridStartY - Pad + rows * (BtnH + BtnPad) + 8)
            .WithFixedPadding(Pad);
        contentBounds.BothSizing = ElementSizing.Fixed;

        var dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.RightMiddle)
            .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);

        var composer = capi.Gui
            .CreateCompo("emotepicker", dialogBounds)
            .AddShadedDialogBG(contentBounds)
            .AddDialogTitleBar(Lang.Get("emotes:dialog-title"), () => TryClose())
            .BeginChildElements(contentBounds);

        composer = AddSelector(composer, dropBounds);

        for (var i = 0; i < names.Length; i++)
        {
            var x = i % Cols * (BtnW + BtnPad);
            var y = gridStartY + i / Cols * (BtnH + BtnPad);
            var capturedCode = codes[i];
            composer.AddSmallButton(names[i], () =>
            {
                if (isVanilla)
                    capi.SendChatMessage("/emote " + capturedCode);
                else
                    ActivateModdedEmote(capturedCode);
                return true;
            }, ElementBounds.Fixed(x, y, BtnW, BtnH), EnumButtonStyle.Normal, $"btn-{i}");
        }

        composer.EndChildElements().Compose();

        SingleComposer = composer;
        FinishSelector(composer);
    }

    private void ComposeAllTab()
    {
        var q = searchText.ToLowerInvariant().Trim();
        var emotes = modSystem.Emotes.Values
            .Where(e => !modSystem.IsEmoteDisabled(e.Code))
            .Select(e => (e.Code, Name: modSystem.GetEmoteName(e)))
            .Where(e => q.Length == 0 || e.Name.ToLowerInvariant().Contains(q) || e.Code.Contains(q))
            .OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        var topY = GuiStyle.TitleBarHeight + Pad;
        var listW = Cols * (BtnW + BtnPad) + 6;
        var bodyW = listW + 33;

        var dropBounds = ElementBounds.Fixed(0, topY, listW, DropH);
        var searchY = useDropDown ? topY + DropH + 5 : topY;

        var contentBounds = ElementBounds.Fixed(0, 0, bodyW, searchY - Pad + SearchH + 5 + AllListH + 8)
            .WithFixedPadding(Pad);
        contentBounds.BothSizing = ElementSizing.Fixed;

        var dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.RightMiddle)
            .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);

        var searchBounds = ElementBounds.Fixed(0, searchY, listW, SearchH);
        var insetBounds = searchBounds.BelowCopy(0, 5).WithFixedSize(listW, AllListH);
        var clipBounds = insetBounds.ForkContainingChild(3, 3, 3, 3);
        allListBounds = clipBounds.ForkContainingChild();
        var scrollbarBounds = ElementStdBounds.VerticalScrollbar(insetBounds);

        var container = new GuiElementContainer(capi, allListBounds);
        for (var i = 0; i < emotes.Length; i++)
        {
            var x = i % Cols * (BtnW + BtnPad);
            var y = i / Cols * (BtnH + BtnPad);
            var code = emotes[i].Code;
            var font = CairoFont.SmallButtonText();
            var hoverFont = CairoFont.SmallButtonText();
            hoverFont.Color = (double[])GuiStyle.ActiveButtonTextColor.Clone();
            var btn = new GuiElementTextButton(capi, emotes[i].Name, font, hoverFont,
                () => { ActivateModdedEmote(code); return true; },
                ElementBounds.Fixed(x, y, BtnW, BtnH), EnumButtonStyle.Normal);
            btn.SetOrientation(font.Orientation);
            container.Add(btn);
        }

        var composer = capi.Gui
            .CreateCompo("emotepicker", dialogBounds)
            .AddShadedDialogBG(contentBounds)
            .AddDialogTitleBar(Lang.Get("emotes:dialog-title"), () => TryClose())
            .BeginChildElements(contentBounds);

        composer = AddSelector(composer, dropBounds);

        composer
            .AddTextInput(searchBounds, OnSearchChanged, CairoFont.WhiteSmallishText(), "search")
            .AddInset(insetBounds, 3)
            .BeginClip(clipBounds)
            .AddInteractiveElement(container, "alllist")
            .EndClip()
            .AddVerticalScrollbar(OnScrollbar, scrollbarBounds, "scrollbar")
            .EndChildElements()
            .Compose();

        SingleComposer = composer;
        FinishSelector(composer);

        composer.GetScrollbar("scrollbar").SetHeights((float)AllListH, (float)allListBounds.fixedHeight);

        var search = composer.GetTextInput("search");
        search.SetPlaceHolderText(Lang.Get("emotes:search-placeholder"));
        if (!string.IsNullOrEmpty(searchText))
            search.SetValue(searchText);
        composer.FocusElement(search.TabIndex);
    }

    private void OnSearchChanged(string text)
    {
        var newText = text ?? "";
        if (newText == searchText) return;

        if (capi.ElapsedMilliseconds - openedMs < 100)
        {
            SingleComposer?.GetTextInput("search")?.SetValue(searchText);
            return;
        }

        searchText = newText;

        if (searchPending) return;
        searchPending = true;
        capi.Event.EnqueueMainThreadTask(() =>
        {
            searchPending = false;
            if (activeKey == AllKey) ComposeAllTab();
        }, "emotes-search");
    }

    private void OnScrollbar(float value)
    {
        if (allListBounds == null) return;
        allListBounds.fixedY = -value;
        allListBounds.CalcWorldBounds();
    }

    private void ActivateModdedEmote(string code)
    {
        if (modSystem.Emotes.TryGetValue(code, out var emote)
            && emote.RequiresTarget
            && cachedEntitySelection is not EntityPlayer and not EntityPlayerBot)
        {
            capi.TriggerIngameError(this, "emote-target-required", Lang.Get("emotes:pair-requires-target"));
            TryClose();
            return;
        }

        modSystem.SendToggleEmote(code);
        TryClose();
    }

    public override void OnGuiOpened()
    {
        cachedEntitySelection = capi.World.Player.CurrentEntitySelection?.Entity;
        searchText = "";
        openedMs = capi.ElapsedMilliseconds;
        ComposeDialog();
    }
}
