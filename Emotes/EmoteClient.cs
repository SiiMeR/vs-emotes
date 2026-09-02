using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Emotes;

public class EmoteClient
{
    private readonly EmotesModSystem system;
    private readonly ICoreClientAPI api;
    private readonly IClientNetworkChannel channel;
    private readonly GuiDialogEmotePicker dialog;

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

        api.Input.RegisterHotKey("emotepicker", Lang.Get("emotes:hotkey-open"), GlKeys.J, shiftPressed: true);
        dialog = new GuiDialogEmotePicker(api, system);
        api.Input.SetHotKeyHandler("emotepicker", _ => ToggleDialog());
    }

    public bool IsDisabled(string code)
    {
        return code != null && disabledEmotes.Contains(code);
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
