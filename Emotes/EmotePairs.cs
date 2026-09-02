using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Emotes;

public class EmotePairs
{
    public const string ConsentKey = "emotesConsent";

    private const double MaxPairDistance = 3.0;

    private readonly EmotesModSystem system;
    private readonly ICoreServerAPI api;
    private readonly IServerNetworkChannel channel;
    private readonly EmotesConfig config;
    private readonly Dictionary<string, PairRequest> requests = new();

    public EmotePairs(EmotesModSystem system, ICoreServerAPI api, IServerNetworkChannel channel, EmotesConfig config)
    {
        this.system = system;
        this.api = api;
        this.channel = channel;
        this.config = config;
    }

    public TextCommandResult Initiate(string emoteCode, IServerPlayer initiatorPlayer)
    {
        if (initiatorPlayer?.Entity is not EntityPlayer initiator)
            return TextCommandResult.Error(Lang.Get("emotes:cmd-only-players"));

        if (!system.Emotes.TryGetValue(emoteCode, out var emote))
            return TextCommandResult.Error(Lang.Get("emotes:cmd-not-found", emoteCode));

        var selected = initiator.EntitySelection?.Entity;
        if (selected == null) return TextCommandResult.Error(Lang.Get("emotes:pair-no-target"));

        if (selected is EntityPlayerBot bot) return StartWithBot(emoteCode, emote, initiatorPlayer, initiator, bot);

        if (selected is not EntityPlayer target) return TextCommandResult.Error(Lang.Get("emotes:pair-no-target"));
        if (target.EntityId == initiator.EntityId) return TextCommandResult.Error(Lang.Get("emotes:pair-self"));

        if (initiator.Pos.DistanceTo(target.Pos) > MaxPairDistance)
            return TextCommandResult.Error(Lang.Get("emotes:pair-too-far"));

        if (EmoteState.InCarry(target)) return TextCommandResult.Error(Lang.Get("emotes:pair-carrying"));

        if (target.Player is not IServerPlayer targetPlayer)
            return TextCommandResult.Error(Lang.Get("emotes:pair-no-target"));

        if (!targetPlayer.GetModData(ConsentKey, config.PairedEmotesRequireConsent))
            return Execute(emoteCode, emote, initiatorPlayer, initiator, targetPlayer, target);

        requests[initiatorPlayer.PlayerUID] = new PairRequest(initiatorPlayer.PlayerUID, targetPlayer.PlayerUID,
            emoteCode, DateTime.Now);

        var accept = "<a href=\"command:///emotes accept\">Accept</a>";
        var refuse = "<a href=\"command:///emotes refuse\">Refuse</a>";
        targetPlayer.SendMessage(GlobalConstants.CurrentChatGroup,
            Lang.Get("emotes:pair-request-received", initiator.GetName(), system.GetEmoteName(emoteCode), accept,
                refuse),
            EnumChatType.GroupInvite);

        return TextCommandResult.Success(Lang.Get("emotes:pair-request-sent", target.GetName()));
    }

    public TextCommandResult Accept(IServerPlayer caller)
    {
        var request = PendingFor(caller);
        if (request == null) return TextCommandResult.Error(Lang.Get("emotes:pair-no-request"));

        requests.Remove(request.InitiatorUid);

        if (api.World.PlayerByUid(request.InitiatorUid) is not IServerPlayer initiatorPlayer)
            return TextCommandResult.Error(Lang.Get("emotes:pair-initiator-gone"));

        if (initiatorPlayer.Entity is not EntityPlayer initiatorEntity ||
            caller.Entity is not EntityPlayer targetEntity)
            return TextCommandResult.Error(Lang.Get("emotes:pair-no-target"));

        if (!system.Emotes.TryGetValue(request.EmoteCode, out var emote))
            return TextCommandResult.Error(Lang.Get("emotes:cmd-not-found", request.EmoteCode));

        return Execute(request.EmoteCode, emote, initiatorPlayer, initiatorEntity, caller, targetEntity);
    }

    public TextCommandResult Refuse(IServerPlayer caller)
    {
        var request = PendingFor(caller);
        if (request == null) return TextCommandResult.Error(Lang.Get("emotes:pair-no-request"));

        requests.Remove(request.InitiatorUid);

        if (api.World.PlayerByUid(request.InitiatorUid) is IServerPlayer initiatorPlayer)
            initiatorPlayer.SendMessage(GlobalConstants.CurrentChatGroup,
                Lang.Get("emotes:pair-refused", caller.Entity?.GetName() ?? caller.PlayerName),
                EnumChatType.Notification);

        return TextCommandResult.Success();
    }

    public void End(EntityPlayer player)
    {
        if (player == null) return;

        var tree = player.WatchedAttributes.GetTreeAttribute(EmoteState.TreeKey);
        var partnerUid = tree?.GetString(EmoteState.PairPartnerKey);
        if (string.IsNullOrEmpty(partnerUid)) return;

        tree.SetString(EmoteState.PairPartnerKey, "");
        player.WatchedAttributes.MarkPathDirty(EmoteState.TreeKey);

        if (api.World.PlayerByUid(partnerUid) is not IServerPlayer partnerPlayer) return;
        if (partnerPlayer.Entity is not EntityPlayer partnerEntity) return;

        EmoteState.Tree(partnerEntity).SetString(EmoteState.PairPartnerKey, "");
        EmoteState.StopAll(partnerEntity);
    }

    private PairRequest PendingFor(IServerPlayer caller)
    {
        if (caller == null) return null;

        return requests.Values
            .OrderByDescending(r => r.RequestTime)
            .FirstOrDefault(r => r.TargetUid == caller.PlayerUID);
    }

    private TextCommandResult StartWithBot(string emoteCode, CustomEmote emote, IServerPlayer initiatorPlayer,
        EntityPlayer initiator, EntityPlayerBot bot)
    {
        var yaw = SnapPositions(initiatorPlayer, initiator, bot, emote.PairDistance);
        if (yaw == null) return TextCommandResult.Error(Lang.Get("emotes:pair-too-far"));

        EmoteState.StopAll(initiator);
        EmoteState.Tree(initiator).SetFloat(EmoteState.PairYawKey, yaw.Value);
        EmoteState.Set(initiator, emoteCode, true);
        return TextCommandResult.Success();
    }

    private TextCommandResult Execute(string emoteCode, CustomEmote emote, IServerPlayer initiatorPlayer,
        EntityPlayer initiatorEntity, IServerPlayer targetPlayer, EntityPlayer targetEntity)
    {
        var yaw = SnapPositions(initiatorPlayer, initiatorEntity, targetEntity, emote.PairDistance);
        if (yaw == null) return TextCommandResult.Error(Lang.Get("emotes:pair-too-far"));

        var targetYaw = yaw.Value + (float)Math.PI;
        channel.SendPacket(new LeanSnapPacket { Yaw = targetYaw }, targetPlayer);

        EmoteState.StopAll(initiatorEntity);
        EmoteState.StopAll(targetEntity);

        var initiatorTree = EmoteState.Tree(initiatorEntity);
        initiatorTree.SetString(EmoteState.PairPartnerKey, targetPlayer.PlayerUID);
        initiatorTree.SetFloat(EmoteState.PairYawKey, yaw.Value);

        var targetTree = EmoteState.Tree(targetEntity);
        targetTree.SetString(EmoteState.PairPartnerKey, initiatorPlayer.PlayerUID);
        targetTree.SetFloat(EmoteState.PairYawKey, targetYaw);

        EmoteState.Set(initiatorEntity, emoteCode, true);
        EmoteState.Set(targetEntity, emoteCode, true);

        return TextCommandResult.Success();
    }

    private float? SnapPositions(IServerPlayer initiatorPlayer, EntityPlayer initiator, Entity target,
        float pairDistance)
    {
        var dx = target.Pos.X - initiator.Pos.X;
        var dz = target.Pos.Z - initiator.Pos.Z;
        var dist = Math.Sqrt(dx * dx + dz * dz);

        if (dist > MaxPairDistance || dist < 0.01) return null;

        var midX = (initiator.Pos.X + target.Pos.X) / 2;
        var midZ = (initiator.Pos.Z + target.Pos.Z) / 2;
        var normX = dx / dist;
        var normZ = dz / dist;

        initiator.TeleportToDouble(midX - normX * pairDistance, initiator.Pos.Y, midZ - normZ * pairDistance);
        target.TeleportToDouble(midX + normX * pairDistance, initiator.Pos.Y, midZ + normZ * pairDistance);

        var yaw = (float)Math.Atan2(dx, dz);
        initiator.Pos.Yaw = yaw;
        target.Pos.Yaw = yaw + (float)Math.PI;

        channel.SendPacket(new LeanSnapPacket { Yaw = yaw }, initiatorPlayer);
        return yaw;
    }

    private record PairRequest(string InitiatorUid, string TargetUid, string EmoteCode, DateTime RequestTime);
}
