using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

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
    private const double HintH = 22;
    private const int Cols = 2;

    private const string AllKey = EmoteCatalog.AllKey;
    private const string FavoritesKey = EmoteCatalog.FavoritesKey;

    private readonly EmoteClient client;
    private readonly EmoteCatalog catalog;
    private readonly EmoteFavorites favorites;
    private string activeKey = AllKey;
    private (string Key, string Label)[] tabs = Array.Empty<(string, string)>();
    private bool useDropDown;
    private double tabWidth = MinTabW;
    private Entity cachedEntitySelection;
    private string searchText = "";
    private bool searchPending;
    private long openedMs;
    private ElementBounds allListBounds;
    private EnumMouseButton lastButton;

    public GuiDialogEmotePicker(ICoreClientAPI capi, EmoteClient client, EmoteCatalog catalog,
        EmoteFavorites favorites) : base(capi)
    {
        this.client = client;
        this.catalog = catalog;
        this.favorites = favorites;
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

    public override void OnMouseUp(MouseEvent args)
    {
        lastButton = args.Button;
        base.OnMouseUp(args);
    }

    private void BuildTabs()
    {
        var list = new List<(string Key, string Label)>
        {
            (AllKey, Lang.Get("emotes:cat-all")),
            (FavoritesKey, Lang.Get("emotes:cat-favorites"))
        };
        list.AddRange(catalog.Categories());
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

        var entries = catalog.Entries(activeKey);
        var rows = Math.Max(1, (entries.Count + Cols - 1) / Cols);
        var contentW = Cols * (BtnW + BtnPad);
        var topY = GuiStyle.TitleBarHeight + Pad;
        var dropBounds = ElementBounds.Fixed(0, topY, contentW, DropH);
        var gridStartY = useDropDown ? topY + DropH + 5 : topY;
        var hintY = gridStartY + rows * (BtnH + BtnPad) + 4;

        var contentBounds = ElementBounds.Fixed(0, 0, contentW, hintY - Pad + HintH + 8)
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

        if (entries.Count == 0 && activeKey == FavoritesKey)
        {
            composer.AddStaticText(Lang.Get("emotes:favorites-empty"), CairoFont.WhiteSmallText(),
                ElementBounds.Fixed(0, gridStartY, contentW, BtnH));
        }

        for (var i = 0; i < entries.Count; i++)
        {
            var x = i % Cols * (BtnW + BtnPad);
            var y = gridStartY + i / Cols * (BtnH + BtnPad);
            var code = entries[i].Code;
            composer.AddSmallButton(favorites.Decorate(code, entries[i].Name), () => OnEmoteClicked(code),
                ElementBounds.Fixed(x, y, BtnW, BtnH), EnumButtonStyle.Normal, $"btn-{i}");
        }

        composer
            .AddStaticText(Lang.Get("emotes:favorites-hint"), CairoFont.WhiteDetailText(),
                ElementBounds.Fixed(0, hintY, contentW, HintH))
            .EndChildElements()
            .Compose();

        SingleComposer = composer;
        FinishSelector(composer);
    }

    private void ComposeAllTab()
    {
        var q = searchText.ToLowerInvariant().Trim();
        var emotes = catalog.Entries(AllKey)
            .Where(e => q.Length == 0 || e.Name.ToLowerInvariant().Contains(q) || e.Code.Contains(q))
            .ToArray();

        var topY = GuiStyle.TitleBarHeight + Pad;
        var listW = Cols * (BtnW + BtnPad) + 6;
        var bodyW = listW + 33;

        var dropBounds = ElementBounds.Fixed(0, topY, listW, DropH);
        var searchY = useDropDown ? topY + DropH + 5 : topY;
        var hintY = searchY + SearchH + 5 + AllListH + 4;

        var contentBounds = ElementBounds.Fixed(0, 0, bodyW, hintY - Pad + HintH + 8)
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
            var btn = new GuiElementTextButton(capi, favorites.Decorate(code, emotes[i].Name), font, hoverFont,
                () => OnEmoteClicked(code),
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
            .AddStaticText(Lang.Get("emotes:favorites-hint"), CairoFont.WhiteDetailText(),
                ElementBounds.Fixed(0, hintY, listW, HintH))
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

    private bool OnEmoteClicked(string code)
    {
        if (lastButton == EnumMouseButton.Right)
        {
            favorites.Toggle(code);
            capi.Event.EnqueueMainThreadTask(ComposeDialog, "emotes-favorite");
            return true;
        }

        client.Play(code, cachedEntitySelection);
        TryClose();
        return true;
    }

    public override void OnGuiOpened()
    {
        cachedEntitySelection = capi.World.Player.CurrentEntitySelection?.Entity;
        searchText = "";
        openedMs = capi.ElapsedMilliseconds;
        ComposeDialog();
    }
}
