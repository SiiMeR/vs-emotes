using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace Emotes;

public class EmotesModSystem : ModSystem
{
    static readonly Dictionary<string, CustomEmote> Emotes = new()
    {
        ["seiza"] = new() { Code = "seiza", Name = "Seiza", Animation = "seiza", StopOnMovement = true,  StopOnDamage = true },

    };

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

    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        api.RegisterEntityBehaviorClass("emotes", typeof(BehaviorEmotes));
        api.Event.OnEntitySpawn += OnEntitySpawn;
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);
        api.ChatCommands
            .GetOrCreate("emotes")
            .RequiresPrivilege(Privilege.chat)
            .WithDescription("Play an emote. Usage: /emotes <name|list|stop>")
            .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"))
            .HandleWith(HandleEmoteCommand);
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
        SetEmoteState(player, emote.Code, !isActive);
        return TextCommandResult.Success(isActive ? $"Stopped emote: {emote.Name}" : $"Started emote: {emote.Name}");
    }
}
