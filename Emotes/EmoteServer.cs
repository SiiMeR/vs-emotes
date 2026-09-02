using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace Emotes;

public class EmoteServer
{
    private readonly EmotesModSystem system;
    private readonly IServerNetworkChannel channel;
    private readonly HashSet<string> disabledEmotes;
    private readonly EmoteCommands commands;

    public EmotePairs Pairs { get; }

    public EmoteServer(EmotesModSystem system, ICoreServerAPI api)
    {
        this.system = system;

        var config = api.LoadModConfig<EmotesConfig>("emotes.json") ?? new EmotesConfig();
        api.StoreModConfig(config, "emotes.json");
        disabledEmotes = new HashSet<string>(config.DisabledEmotes ?? new List<string>(),
            StringComparer.OrdinalIgnoreCase);

        channel = api.Network.RegisterChannel(EmotesModSystem.ChannelName)
            .RegisterMessageType<EmotePacket>()
            .RegisterMessageType<LeanSnapPacket>()
            .RegisterMessageType<DisabledEmotesPacket>()
            .SetMessageHandler<EmotePacket>(OnEmotePacket);

        Pairs = new EmotePairs(system, api, channel, config);
        commands = new EmoteCommands(system, api, this);
        commands.Register();

        api.Event.PlayerJoin += SendDisabledEmotes;
    }

    public bool IsDisabled(string code)
    {
        return code != null && disabledEmotes.Contains(code);
    }

    private void SendDisabledEmotes(IServerPlayer player)
    {
        channel?.SendPacket(new DisabledEmotesPacket { Codes = disabledEmotes.ToArray() }, player);
    }

    private void OnEmotePacket(IServerPlayer fromPlayer, EmotePacket packet)
    {
        if (fromPlayer?.Entity is not EntityPlayer player) return;

        if (packet.ForceStop)
        {
            EmoteState.StopAll(player);
            return;
        }

        if (string.IsNullOrEmpty(packet.Code)) return;
        if (!system.Emotes.TryGetValue(packet.Code, out var emote)) return;
        if (IsDisabled(packet.Code)) return;
        if (EmoteState.InCarry(player)) return;

        if (emote.RequiresTarget)
        {
            Pairs.Initiate(packet.Code, fromPlayer);
            return;
        }

        if (player.MountedOn != null) return;

        var tree = EmoteState.Tree(player);
        var starting = !tree.GetBool(packet.Code);

        if (starting && emote.SnapToWall) SnapToWall(fromPlayer, tree);
        else tree.RemoveAttribute(EmoteState.LeanYawKey);

        EmoteState.Toggle(player, packet.Code);
    }

    private void SnapToWall(IServerPlayer player, ITreeAttribute tree)
    {
        var yaw = EmoteWallSnap.TrySnap(player.Entity);
        if (yaw == null)
        {
            tree.RemoveAttribute(EmoteState.LeanYawKey);
            return;
        }

        tree.SetFloat(EmoteState.LeanYawKey, yaw.Value);
        channel.SendPacket(new LeanSnapPacket { Yaw = yaw.Value }, player);
    }
}
