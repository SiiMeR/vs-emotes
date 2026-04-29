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
    private const int Cols = 2;

    private static readonly string[] VanillaEmoteCodes =
        { "wave", "cheer", "shrug", "cry", "nod", "facepalm", "bow", "laugh", "rage" };

    private static readonly (string LangKey, string[] Codes)[] NamedCategories =
    {
        ("cat-sitting",    new[] { "seiza", "prayer", "sittingcool", "sittingchill", "sittingrelaxed", "sittingrefined", "sittingcalm", "sittinginnocent", "squatting", "kneel" }),
        ("cat-laying",     new[] { "layingdown", "layingback", "laydownsensual" }),
        ("cat-friendly",   new[] { "blowkiss", "clapping", "dapup", "kisshand", "politebow", "victory" }),
        ("cat-neutral",    new[] { "atease", "crossedarms", "crossedarmsthinking", "handrub", "handships", "handup", "knocking", "leaningcrossed", "leaninghandshead", "leaninghips", "martialarts", "noblesalute", "pointing", "prayerstanding", "refinedsalute", "salute", "scanning", "surrender", "thinkinghard", "crackingknuckles" }),
        ("cat-aggressive", new[] { "bringiton", "chestthump", "engarde", "flippingoff", "slitthroat" }),
        ("cat-paired",     new[] { "hug", "hugfriendly", "handshake", "kiss", "smooch" }),
    };

    private static int VanillaIndex => NamedCategories.Length;
    private static int TabCount     => NamedCategories.Length + 1;

    private readonly EmotesModSystem modSystem;
    private int activeCategory;
    private Entity cachedEntitySelection;

    public GuiDialogEmotePicker(ICoreClientAPI capi, EmotesModSystem modSystem) : base(capi)
    {
        this.modSystem = modSystem;
    }

    public override string ToggleKeyCombinationCode => "emotepicker";
    public override bool PrefersUngrabbedMouse => false;
    public override bool ShouldReceiveKeyboardEvents() => false;

    private void ComposeDialog()
    {
        bool isVanilla = activeCategory == VanillaIndex;

        string[] codes, names;
        if (isVanilla)
        {
            codes = VanillaEmoteCodes;
            names = codes.Select(c => Lang.Get("emotes:emote-" + c)).ToArray();
        }
        else
        {
            var emotes = NamedCategories[activeCategory].Codes
                .Where(c => EmotesModSystem.GetEmotes().ContainsKey(c))
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

        var guiTabs = new GuiTab[TabCount];
        for (int i = 0; i < NamedCategories.Length; i++)
            guiTabs[i] = new GuiTab { Name = Lang.Get("emotes:" + NamedCategories[i].LangKey), DataInt = i };
        guiTabs[VanillaIndex] = new GuiTab { Name = Lang.Get("emotes:cat-vanilla"), DataInt = VanillaIndex };

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
                {
                    capi.SendChatMessage("/emote " + capturedCode);
                }
                else
                {
                    if (EmotesModSystem.GetEmotes().TryGetValue(capturedCode, out var emote)
                        && emote.RequiresTarget
                        && cachedEntitySelection is not EntityPlayer and not EntityPlayerBot)
                    {
                        capi.TriggerIngameError(this, "emote-target-required", Lang.Get("emotes:pair-requires-target"));
                        TryClose();
                        return true;
                    }
                    modSystem.SendToggleEmote(capturedCode);
                }
                TryClose();
                return true;
            }, ElementBounds.Fixed(x, y, BtnW, BtnH), EnumButtonStyle.Normal, $"btn-{i}");
        }

        composer.EndChildElements().Compose();

        for (int i = 0; i < guiTabs.Length; i++) guiTabs[i].Active = false;
        guiTabs[activeCategory].Active = true;
        composer.GetVerticalTab("tabs").ActiveElement = activeCategory;

        SingleComposer = composer;
    }

    public override void OnGuiOpened()
    {
        cachedEntitySelection = capi.World.Player.CurrentEntitySelection?.Entity;
        ComposeDialog();
    }
}
