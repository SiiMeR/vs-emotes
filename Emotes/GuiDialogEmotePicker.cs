using System.Linq;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace Emotes;

public class GuiDialogEmotePicker : GuiDialog
{
    private const double Pad = 10;
    private const double TabH = 30;
    private const double TabW = 120;
    private const double BtnW = 195;
    private const double BtnH = 36;
    private const double BtnPad = 3;
    private const int Cols = 2;

    private static readonly string[] VanillaEmoteCodes = { "wave", "cheer", "shrug", "cry", "nod", "facepalm", "bow", "laugh", "rage" };

    private readonly EmotesModSystem modSystem;

    private int activeTab;

    public GuiDialogEmotePicker(ICoreClientAPI capi, EmotesModSystem modSystem) : base(capi)
    {
        this.modSystem = modSystem;
    }

    public override string ToggleKeyCombinationCode => "emotepicker";
    public override bool PrefersUngrabbedMouse => false;

    public override bool ShouldReceiveKeyboardEvents()
    {
        return false;
    }

    private void ComposeDialog()
    {
        string[] codes;
        string[] names;

        if (activeTab == 1)
        {
            codes = VanillaEmoteCodes;
            names = VanillaEmoteCodes.Select(c => Lang.Get("emotes:emote-" + c)).ToArray();
        }
        else
        {
            var emotes = EmotesModSystem.GetEmotes().Values
                .Select(e => (e.Code, Name: Lang.Get("emotes:emote-" + e.Code)))
                .OrderBy(e => e.Name)
                .ToArray();
            codes = emotes.Select(e => e.Code).ToArray();
            names = emotes.Select(e => e.Name).ToArray();
        }

        const double TabGap = 8;
        const double BottomPad = 8;

        var rows = (names.Length + Cols - 1) / Cols;
        var contentW = Cols * (BtnW + BtnPad);
        var contentH = rows * (BtnH + BtnPad);
        var gridStartY = GuiStyle.TitleBarHeight + TabH + TabGap;

        var tabs = new GuiTab[]
        {
            new() { Name = Lang.Get("emotes:tab-custom"), DataInt = 0 },
            new() { Name = Lang.Get("emotes:tab-vanilla"), DataInt = 1 }
        };

        var tabsBounds = ElementBounds.Fixed(0, GuiStyle.TitleBarHeight, tabs.Length * TabW, TabH);
        var contentBounds = ElementBounds.Fixed(0, 0, contentW, gridStartY - Pad + contentH + BottomPad)
            .WithFixedPadding(Pad);
        contentBounds.BothSizing = ElementSizing.Fixed;

        var dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.RightMiddle)
            .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);

        var font = CairoFont.WhiteSmallText();
        var selectedFont = CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold);
        var btnFont = CairoFont.SmallButtonText();

        var composer = capi.Gui
            .CreateCompo("emotepicker", dialogBounds)
            .AddShadedDialogBG(contentBounds)
            .AddDialogTitleBar(Lang.Get("emotes:dialog-title"), () => TryClose())
            .BeginChildElements(contentBounds)
            .AddHorizontalTabs(tabs, tabsBounds, OnTabClicked, font, selectedFont, "tabs");

        composer.GetHorizontalTabs("tabs").activeElement = activeTab;

        for (var i = 0; i < names.Length; i++)
        {
            var col = i % Cols;
            var row = i / Cols;
            var x = col * (BtnW + BtnPad);
            var y = gridStartY + row * (BtnH + BtnPad);
            var btnBounds = ElementBounds.Fixed(x, y, BtnW, BtnH);
            var captured = i;
            var capturedCode = codes[i];
            composer.AddSmallButton(names[i], () =>
                {
                    if (activeTab == 1)
                    {
                        capi.SendChatMessage("/emote " + capturedCode);
                    }
                    else
                    {
                        modSystem.SendToggleEmote(capturedCode);
                    }

                    TryClose();
                    return true;
                }, btnBounds, EnumButtonStyle.Normal, $"btn-{i}");
        }

        composer.EndChildElements().Compose();
        SingleComposer = composer;
    }

    private void OnTabClicked(int tabDataInt)
    {
        activeTab = tabDataInt;
        ComposeDialog();
    }

    public override void OnGuiOpened()
    {
        ComposeDialog();
    }
}