using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Emotes;

public class EmoteClient
{
    private readonly EmotesModSystem system;
    private readonly ICoreClientAPI api;
    private readonly IClientNetworkChannel channel;
    private readonly GuiDialogEmotePicker dialog;
    private readonly EmoteFavorites favorites;
    private readonly EmoteCatalog catalog;
    private readonly EmoteWheel wheel;

    private HashSet<string> disabledEmotes = new(StringComparer.OrdinalIgnoreCase);

    public EmoteClient(EmotesModSystem system, ICoreClientAPI api)
    {
        this.system = system;
        this.api = api;

        channel = api.Network.RegisterChannel(EmotesModSystem.ChannelName)
            .RegisterMessageType<EmotePacket>()
            .RegisterMessageType<LeanSnapPacket>()
            .RegisterMessageType<DisabledEmotesPacket>()
            .SetMessageHandler<LeanSnapPacket>(OnLeanSnap)
            .SetMessageHandler<DisabledEmotesPacket>(OnDisabledEmotes);

        favorites = new EmoteFavorites(api);
        catalog = new EmoteCatalog(api, system, favorites);

        api.Input.RegisterHotKey("emotepicker", Lang.Get("emotes:hotkey-open"), GlKeys.J, shiftPressed: true);
        dialog = new GuiDialogEmotePicker(api, this, catalog, favorites);
        api.Input.SetHotKeyHandler("emotepicker", _ => ToggleDialog());

        wheel = new EmoteWheel(api, this, catalog, favorites);
        WheelMouseGrabPatch.Apply();
    }

    public bool WheelOpen => wheel?.Opened == true;

    public bool DialogOpen => dialog.IsOpened();

    public bool IsDisabled(string code)
    {
        return code != null && disabledEmotes.Contains(code);
    }

    public bool Play(string code, Entity target)
    {
        if (string.IsNullOrEmpty(code)) return false;

        if (!system.Emotes.TryGetValue(code, out var emote))
        {
            if (EmoteState.IsEmoting(api.World.Player?.Entity)) SendStop();
            api.SendChatMessage("/emote " + code);
            return true;
        }

        if (emote.RequiresTarget
            && target is not EntityPlayer and not EntityPlayerBot
            && !EmotePairs.IsEntityPartner(target))
        {
            api.TriggerIngameError(this, "emote-target-required", Lang.Get("emotes:pair-requires-target"));
            return false;
        }

        SendToggle(code);
        return true;
    }

    public void Dispose()
    {
        wheel?.Dispose();
    }

    public void SendToggle(string code)
    {
        var self = api.World.Player?.Entity;

        if (EmoteState.InCarry(self))
        {
            api.TriggerIngameError(this, "emote-carrying", Lang.Get("emotes:cmd-carrying"));
            return;
        }

        if (self?.MountedOn != null)
        {
            api.TriggerIngameError(this, "emote-mounted", Lang.Get("emotes:cmd-mounted"));
            return;
        }

        channel?.SendPacket(new EmotePacket { Code = code });
    }

    public void SendStop()
    {
        channel?.SendPacket(new EmotePacket { ForceStop = true });
    }

    private bool ToggleDialog()
    {
        if (WheelOpen) return true;
        if (dialog.IsOpened()) dialog.TryClose();
        else dialog.TryOpen();
        return true;
    }

    private void OnLeanSnap(LeanSnapPacket packet)
    {
        if (api.World.Player?.Entity is not EntityPlayer player) return;
        player.BodyYawLimits = new AngleConstraint(packet.Yaw, 0f);
    }

    private void OnDisabledEmotes(DisabledEmotesPacket packet)
    {
        disabledEmotes = new HashSet<string>(packet.Codes ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        system.FilterInjectedAnimations(disabledEmotes);
    }
}
