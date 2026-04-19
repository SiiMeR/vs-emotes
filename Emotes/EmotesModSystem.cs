using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace Emotes;

public class EmotesModSystem : ModSystem
{
    const string ChannelName = "emotes";

    static readonly Dictionary<string, CustomEmote> Emotes = new()
    {
        ["seiza"] = new() { Code = "seiza", Name = "Seiza", Animation = "seiza", StopOnMovement = true, StopOnDamage = true },
        ["kneel"] = new() { Code = "kneel", Name = "Kneel", Animation = "kneel", StopOnMovement = true, StopOnDamage = true },
        ["layingdown"] = new() { Code = "layingdown", Name = "LayingDown", Animation = "layingdown", StopOnMovement = true, StopOnDamage = true },
        ["surrender"] = new() { Code = "surrender", Name = "Surrender", Animation = "surrender", StopOnMovement = true, StopOnDamage = true },
        ["atease"] = new() { Code = "atease", Name = "AtEase", Animation = "atease", StopOnMovement = false, StopOnDamage = true },
        ["pointing"] = new() { Code = "pointing", Name = "Pointing", Animation = "pointing", StopOnMovement = true, StopOnDamage = true },
        ["leaningcrossed"] = new() { Code = "leaningcrossed", Name = "LeaningCrossed", Animation = "leaningcrossed", StopOnMovement = true, StopOnDamage = true },
        ["leaninghips"] = new() { Code = "leaninghips", Name = "LeaningHips", Animation = "leaninghips", StopOnMovement = true, StopOnDamage = true },
        ["crossedarmsleaning2"] = new() { Code = "crossedarmsleaning2", Name = "CrossedArmsLeaning2", Animation = "crossedarmsleaning2", StopOnMovement = true, StopOnDamage = true },
        ["flippingoff"] = new() { Code = "flippingoff", Name = "FlippingOff", Animation = "flippingoff", StopOnMovement = true, StopOnDamage = true },
        ["crossedarmsthinking"] = new() { Code = "crossedarmsthinking", Name = "CrossedArmsThinking", Animation = "crossedarmsthinking", StopOnMovement = true, StopOnDamage = true },
        ["sittingcool"] = new() { Code = "sittingcool", Name = "SittingCool", Animation = "sittingcool", StopOnMovement = true, StopOnDamage = true },
        ["blowkiss"] = new() { Code = "blowkiss", Name = "Blowkiss", Animation = "blowkiss", StopOnMovement = true, StopOnDamage = true },
        ["chestthump"] = new() { Code = "chestthump", Name = "ChestThump", Animation = "chestthump", StopOnMovement = true, StopOnDamage = true },
        ["clapping"] = new() { Code = "clapping", Name = "Clapping", Animation = "clapping", StopOnMovement = true, StopOnDamage = true },
        ["crossedarms"] = new() { Code = "crossedarms", Name = "CrossedArms", Animation = "crossedarms", StopOnMovement = true, StopOnDamage = true },
        ["handshake"] = new() { Code = "handshake", Name = "Handshake", Animation = "handshake", StopOnMovement = true, StopOnDamage = true },
        ["layingback"] = new() { Code = "layingback", Name = "LayingBack", Animation = "layingback", StopOnMovement = true, StopOnDamage = true },
        ["refinedsalute"] = new() { Code = "refinedsalute", Name = "RefinedSalute", Animation = "refinedsalute", StopOnMovement = true, StopOnDamage = true },
        ["salute"] = new() { Code = "salute", Name = "Salute", Animation = "salute", StopOnMovement = true, StopOnDamage = true },
        ["scanning"] = new() { Code = "scanning", Name = "Scanning", Animation = "scanning", StopOnMovement = true, StopOnDamage = true },
        ["squatting"] = new() { Code = "squatting", Name = "Squatting", Animation = "squatting", StopOnMovement = true, StopOnDamage = true },
        ["thinkinghard"] = new() { Code = "thinkinghard", Name = "ThinkingHard", Animation = "thinkinghard", StopOnMovement = true, StopOnDamage = true },
        ["bringiton"] = new() { Code = "bringiton", Name = "BringItOn", Animation = "bringiton", StopOnMovement = true, StopOnDamage = true },
        ["slitthroat"] = new() { Code = "slitthroat", Name = "SlitThroat", Animation = "slitthroat", StopOnMovement = true, StopOnDamage = true },
    };

    IClientNetworkChannel clientChannel;

    public static IReadOnlyDictionary<string, CustomEmote> GetEmotes() => Emotes;

    public static void SetEmoteState(EntityPlayer player, string code, bool active)
    {
        var tree = player.WatchedAttributes.GetOrAddTreeAttribute("emotes");
        tree.SetBool(code, active);
        player.WatchedAttributes.MarkPathDirty("emotes");
    }

    public static void StopAllEmotes(EntityPlayer player)
    {
        var tree = player.WatchedAttributes.GetOrAddTreeAttribute("emotes");
        foreach (var code in Emotes.Keys)
            tree.SetBool(code, false);
        player.WatchedAttributes.MarkPathDirty("emotes");
    }

    public void SendToggleEmote(string code)
    {
        clientChannel?.SendPacket(new EmotePacket { Code = code });
    }

    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        api.RegisterEntityBehaviorClass("emotes", typeof(BehaviorEmotes));
        api.Event.OnEntitySpawn += OnEntitySpawn;
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);

        clientChannel = api.Network.RegisterChannel(ChannelName)
            .RegisterMessageType<EmotePacket>();

        api.Input.RegisterHotKey("emotepicker", "Open Emote Picker", GlKeys.J, HotkeyType.CharacterControls, shiftPressed: true);
        var dialog = new GuiDialogEmotePicker(api, this);
        api.Input.SetHotKeyHandler("emotepicker", _ =>
        {
            if (dialog.IsOpened()) dialog.TryClose();
            else dialog.TryOpen();
            return true;
        });
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);

        api.Network.RegisterChannel(ChannelName)
            .RegisterMessageType<EmotePacket>()
            .SetMessageHandler<EmotePacket>(OnEmotePacket);

        api.ChatCommands
            .GetOrCreate("emotes")
            .RequiresPrivilege(Privilege.chat)
            .WithDescription("Play an emote. Usage: /emotes <name|list|stop>")
            .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"))
            .HandleWith(HandleEmoteCommand);
    }

    void OnEmotePacket(IServerPlayer fromPlayer, EmotePacket packet)
    {
        if (fromPlayer.Entity is not EntityPlayer player) return;
        if (!Emotes.ContainsKey(packet.Code)) return;
        var tree = player.WatchedAttributes.GetOrAddTreeAttribute("emotes");
        bool isActive = tree.GetBool(packet.Code);
        if (!isActive) StopAllEmotes(player);
        SetEmoteState(player, packet.Code, !isActive);
    }

    void OnEntitySpawn(Entity entity)
    {
        if (entity is not EntityPlayer) return;
        if (entity.GetBehavior<BehaviorEmotes>() != null) return;
        var behavior = new BehaviorEmotes(entity);
        entity.AddBehavior(behavior);
        behavior.Initialize(entity.Properties, null);
    }

    TextCommandResult HandleEmoteCommand(TextCommandCallingArgs args)
    {
        if (args.Caller.Entity is not EntityPlayer player)
            return TextCommandResult.Error("Only players can use emotes");

        var input = (string)args[0];

        if (string.IsNullOrEmpty(input) || input.Equals("list", System.StringComparison.OrdinalIgnoreCase))
            return TextCommandResult.Success("Available emotes: " + string.Join(", ", Emotes.Keys));

        if (input.Equals("stop", System.StringComparison.OrdinalIgnoreCase) ||
            input.Equals("stopall", System.StringComparison.OrdinalIgnoreCase))
        {
            StopAllEmotes(player);
            return TextCommandResult.Success("All emotes stopped");
        }

        var key = input.ToLowerInvariant();
        if (!Emotes.TryGetValue(key, out var emote))
            return TextCommandResult.Error($"Emote '{input}' not found. Use '/emotes list' to see available emotes.");

        var tree = player.WatchedAttributes.GetOrAddTreeAttribute("emotes");
        bool isActive = tree.GetBool(emote.Code);
        if (!isActive) StopAllEmotes(player);
        SetEmoteState(player, emote.Code, !isActive);
        return TextCommandResult.Success(isActive ? $"Stopped emote: {emote.Name}" : $"Started emote: {emote.Name}");
    }
}
