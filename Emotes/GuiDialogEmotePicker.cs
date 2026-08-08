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
    private const double TabW = 100;
    private const double BtnW = 195;
    private const double BtnH = 36;
    private const double BtnPad = 3;
    private const double SearchH = 30;
    private const double AllListH = 400;
    private const int Cols = 2;

    private static readonly string[] VanillaEmoteCodes =
        { "wave", "cheer", "shrug", "cry", "nod", "facepalm", "bow", "laugh", "rage" };

    private static readonly (string LangKey, string[] Codes)[] NamedCategories =
    {
        ("cat-sitting",    new[] { "seiza", "prayer", "sittingcool", "sittingchill", "sittingrelaxed", "sittingrefined", "sittingcalm", "sittinginnocent", "squatting", "kneel", "sittingrested", "sittingintrovert" }),
        ("cat-laying",     new[] { "layingdown", "prone", "playdead", "layingback", "laydownsensual" }),
        ("cat-friendly",   new[] { "blowkiss", "clapping", "kisshand", "politebow", "victory" }),
        ("cat-idle",       new[] { "atease", "crossedarms", "handships", "leaningcrossed", "leaninghandshead", "leaninghips", "leaningsimple", "leaningoversimple", "leaningoverconfident" }),
        ("cat-neutral",    new[] { "crossedarmsthinking", "handrub", "handup", "knocking", "martialarts", "noblesalute", "pointing", "prayerstanding", "refinedsalute", "salute", "scanning", "surrender", "thinkinghard", "crackingknuckles" }),
        ("cat-aggressive", new[] { "bringiton", "chestthump", "engarde", "flippingoff", "slitthroat" }),
        ("cat-paired",     new[] { "hug", "hugfriendly", "handshake", "dapup", "kiss", "smooch", "handshakedouble", "handholding", "handholdingintimate" }),
    };

    private static int AllIndex     => 0;
    private static int VanillaIndex => NamedCategories.Length + 1;
    private static int TabCount     => NamedCategories.Length + 2;

    private readonly EmotesModSystem modSystem;
    private int activeCategory;
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
    public override bool PrefersUngrabbedMouse => false;
    public override bool ShouldReceiveKeyboardEvents() => IsOpened() && activeCategory == AllIndex;

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

    private static string GetVanillaEmoteName(string code)
    {
        var key = "emotes:emote-" + code;
        if (Lang.HasTranslation(key)) return Lang.Get(key);
        return char.ToUpperInvariant(code[0]) + code.Substring(1);
    }

    private GuiTab[] BuildTabs()
    {
        var tabs = new GuiTab[TabCount];
        tabs[AllIndex] = new GuiTab { Name = Lang.Get("emotes:cat-all"), DataInt = AllIndex };
        for (int i = 0; i < NamedCategories.Length; i++)
            tabs[i + 1] = new GuiTab { Name = Lang.Get("emotes:" + NamedCategories[i].LangKey), DataInt = i + 1 };
        tabs[VanillaIndex] = new GuiTab { Name = Lang.Get("emotes:cat-vanilla"), DataInt = VanillaIndex };
        return tabs;
    }

    private void FinishTabs(GuiComposer composer, GuiTab[] guiTabs)
    {
        for (int i = 0; i < guiTabs.Length; i++) guiTabs[i].Active = false;
        guiTabs[activeCategory].Active = true;
        composer.GetVerticalTab("tabs").ActiveElement = activeCategory;
    }

    private void ComposeDialog()
    {
        if (activeCategory == AllIndex)
        {
            ComposeAllTab();
            return;
        }

        bool isVanilla = activeCategory == VanillaIndex;

        string[] codes, names;
        if (isVanilla)
        {
            codes = GetVanillaEmoteCodes();
            names = codes.Select(GetVanillaEmoteName).ToArray();
        }
        else
        {
            var emotes = NamedCategories[activeCategory - 1].Codes
                .Where(c => EmotesModSystem.GetEmotes().ContainsKey(c) && !modSystem.IsEmoteDisabled(c))
                .Select(c => (Code: c, Name: Lang.Get("emotes:emote-" + c)))
                .OrderBy(e => e.Name)
                .ToArray();
            codes = emotes.Select(e => e.Code).ToArray();
            names = emotes.Select(e => e.Name).ToArray();
        }

        var rows = System.Math.Max(1, (names.Length + Cols - 1) / Cols);
        var contentW = Cols * (BtnW + BtnPad);
        var gridStartY = GuiStyle.TitleBarHeight + Pad;

        var contentBounds = ElementBounds.Fixed(0, 0, contentW, gridStartY - Pad + rows * (BtnH + BtnPad) + 8)
            .WithFixedPadding(Pad);
        contentBounds.BothSizing = ElementSizing.Fixed;

        var dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.RightMiddle)
            .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);

        var tabBounds = ElementBounds.Fixed(-(Pad + TabW), GuiStyle.TitleBarHeight, TabW, TabCount * 32.0);

        var guiTabs = BuildTabs();

        var composer = capi.Gui
            .CreateCompo("emotepicker", dialogBounds)
            .AddShadedDialogBG(contentBounds)
            .AddDialogTitleBar(Lang.Get("emotes:dialog-title"), () => TryClose())
            .BeginChildElements(contentBounds)
            .AddVerticalTabs(guiTabs, tabBounds, (idx, _) => { activeCategory = idx; ComposeDialog(); }, "tabs");

        for (var i = 0; i < names.Length; i++)
        {
            var x = (i % Cols) * (BtnW + BtnPad);
            var y = gridStartY + (i / Cols) * (BtnH + BtnPad);
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
        FinishTabs(composer, guiTabs);
    }

    private void ComposeAllTab()
    {
        var q = searchText.ToLowerInvariant().Trim();
        var emotes = EmotesModSystem.GetEmotes()
            .Where(kv => !modSystem.IsEmoteDisabled(kv.Key))
            .Select(kv => (Code: kv.Key, Name: Lang.Get("emotes:emote-" + kv.Key)))
            .Where(e => q.Length == 0 || e.Name.ToLowerInvariant().Contains(q) || e.Code.Contains(q))
            .OrderBy(e => e.Name)
            .ToArray();

        var gridStartY = GuiStyle.TitleBarHeight + Pad;
        var listW = Cols * (BtnW + BtnPad) + 6;
        var bodyW = listW + 33;

        var contentBounds = ElementBounds.Fixed(0, 0, bodyW, gridStartY - Pad + SearchH + 5 + AllListH + 8)
            .WithFixedPadding(Pad);
        contentBounds.BothSizing = ElementSizing.Fixed;

        var dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.RightMiddle)
            .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);

        var tabBounds = ElementBounds.Fixed(-(Pad + TabW), GuiStyle.TitleBarHeight, TabW, TabCount * 32.0);

        var searchBounds = ElementBounds.Fixed(0, gridStartY, listW, SearchH);
        var insetBounds = searchBounds.BelowCopy(0, 5).WithFixedSize(listW, AllListH);
        var clipBounds = insetBounds.ForkContainingChild(3, 3, 3, 3);
        allListBounds = clipBounds.ForkContainingChild();
        var scrollbarBounds = ElementStdBounds.VerticalScrollbar(insetBounds);

        var container = new GuiElementContainer(capi, allListBounds);
        for (var i = 0; i < emotes.Length; i++)
        {
            var x = (i % Cols) * (BtnW + BtnPad);
            var y = (i / Cols) * (BtnH + BtnPad);
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

        var guiTabs = BuildTabs();

        var composer = capi.Gui
            .CreateCompo("emotepicker", dialogBounds)
            .AddShadedDialogBG(contentBounds)
            .AddDialogTitleBar(Lang.Get("emotes:dialog-title"), () => TryClose())
            .BeginChildElements(contentBounds)
            .AddVerticalTabs(guiTabs, tabBounds, (idx, _) => { activeCategory = idx; ComposeDialog(); }, "tabs")
            .AddTextInput(searchBounds, OnSearchChanged, CairoFont.WhiteSmallishText(), "search")
            .AddInset(insetBounds, 3)
            .BeginClip(clipBounds)
            .AddInteractiveElement(container, "alllist")
            .EndClip()
            .AddVerticalScrollbar(OnScrollbar, scrollbarBounds, "scrollbar")
            .EndChildElements()
            .Compose();

        SingleComposer = composer;
        FinishTabs(composer, guiTabs);

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
            if (activeCategory == AllIndex) ComposeAllTab();
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
        if (EmotesModSystem.GetEmotes().TryGetValue(code, out var emote)
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
