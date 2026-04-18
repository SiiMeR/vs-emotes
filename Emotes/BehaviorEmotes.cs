using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace Emotes;

public class BehaviorEmotes : EntityBehavior
{
    ICoreAPI api;

    public BehaviorEmotes(Entity entity) : base(entity) { }

    public override string PropertyName() => "emotes";

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        base.Initialize(properties, attributes);
        api = entity.Api;

        if (api.Side == EnumAppSide.Server && entity is EntityPlayer player)
            EmotesModSystem.StopAllEmotes(player);

        if (api.Side != EnumAppSide.Client) return;

        entity.WatchedAttributes.RegisterModifiedListener("emotes", SyncAnimations);
    }

    void SyncAnimations()
    {
        var tree = entity.WatchedAttributes.GetTreeAttribute("emotes");
        foreach (var (code, emote) in EmotesModSystem.GetEmotes())
        {
            bool shouldPlay = tree?.GetBool(code) ?? false;
            bool isPlaying = entity.AnimManager?.IsAnimationActive(emote.Animation) ?? false;

            if (shouldPlay && !isPlaying)
                StartAnimation(emote);
            else if (!shouldPlay && isPlaying)
                entity.AnimManager?.StopAnimation(emote.Animation);
        }
    }

    void StartAnimation(CustomEmote emote)
    {
        if (entity.Properties.Client?.AnimationsByMetaCode == null) return;
        if (!entity.Properties.Client.AnimationsByMetaCode.TryGetValue(emote.Animation, out var meta)) return;
        var clone = meta.Clone();
        clone.ClientSide = true;
        entity.AnimManager?.StartAnimation(clone);
    }

    public override void OnGameTick(float deltaTime)
    {
        if (api.Side != EnumAppSide.Server) return;
        if (entity is not EntityPlayer player) return;
        var controls = (entity as EntityAgent)?.Controls;
        if (controls == null) return;
        if (!controls.TriesToMove && !controls.Jump && !controls.Sneak && !controls.Sprint) return;

        var tree = player.WatchedAttributes.GetTreeAttribute("emotes");
        if (tree == null) return;

        bool anyChanged = false;
        foreach (var (code, emote) in EmotesModSystem.GetEmotes())
        {
            if (!emote.StopOnMovement || !tree.GetBool(code)) continue;
            tree.SetBool(code, false);
            anyChanged = true;
        }

        if (anyChanged)
            player.WatchedAttributes.MarkPathDirty("emotes");
    }

    public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
    {
        base.OnEntityReceiveDamage(damageSource, ref damage);

        if (api.Side != EnumAppSide.Server) return;
        if (entity is not EntityPlayer player) return;

        var tree = player.WatchedAttributes.GetTreeAttribute("emotes");
        if (tree == null) return;

        bool anyChanged = false;
        foreach (var (code, emote) in EmotesModSystem.GetEmotes())
        {
            if (!emote.StopOnDamage || !tree.GetBool(code)) continue;
            tree.SetBool(code, false);
            anyChanged = true;
        }

        if (anyChanged)
            player.WatchedAttributes.MarkPathDirty("emotes");
    }

    public override void OnEntityDeath(DamageSource damageSource)
    {
        base.OnEntityDeath(damageSource);

        if (api.Side != EnumAppSide.Server) return;
        if (entity is not EntityPlayer player) return;
        EmotesModSystem.StopAllEmotes(player);
    }
}
