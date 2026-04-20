using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Emotes;

public class BehaviorEmotes : EntityBehavior
{
    ICoreAPI api;
    IRenderer fixYawRenderer;
    float lockedYaw;
    bool yawLocked;

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

        if (entity.AnimManager != null)
            entity.AnimManager.OnAnimationStopped += OnAnimationStopped;
    }

    void OnAnimationStopped(string animCode)
    {
        var capi = api as ICoreClientAPI;
        if (capi?.World.Player?.Entity?.EntityId != entity.EntityId) return;

        foreach (var (code, emote) in EmotesModSystem.GetEmotes())
        {
            if (emote.Animation != animCode) continue;
            var tree = entity.WatchedAttributes.GetTreeAttribute("emotes");
            if (tree?.GetBool(code) != true) continue;

            if (emote.StopAfterAnimation)
                api.ModLoader.GetModSystem<EmotesModSystem>().SendToggleEmote(code);
            break;
        }
    }

    void SyncAnimations()
    {
        var tree = entity.WatchedAttributes.GetTreeAttribute("emotes");

        bool anyActive = false;
        foreach (var (code, emote) in EmotesModSystem.GetEmotes())
        {
            bool shouldPlay = tree?.GetBool(code) ?? false;
            bool isPlaying = entity.AnimManager?.IsAnimationActive(emote.Animation) ?? false;

            if (shouldPlay && !isPlaying)
                StartAnimation(emote);
            else if (!shouldPlay && isPlaying)
                entity.AnimManager?.StopAnimation(emote.Animation);

            if (shouldPlay) anyActive = true;
        }

        if (anyActive && !yawLocked) LockYaw();
        else if (!anyActive && yawLocked) UnlockYaw();
    }

    void LockYaw()
    {
        yawLocked = true;

        if (entity is EntityPlayer ep)
        {
            if (ep.BodyYawLimits != null)
                lockedYaw = ep.BodyYawLimits.CenterRad;
            else
            {
                lockedYaw = entity.Pos.Yaw;
                ep.BodyYawLimits = new AngleConstraint(lockedYaw, 0f);
            }
        }
        else
        {
            lockedYaw = entity.Pos.Yaw;
        }

        if (api is not ICoreClientAPI capi) return;
        bool isSelf = capi.World.Player?.Entity?.EntityId == entity.EntityId;
        if (isSelf) return;

        var capturedEntity = entity;
        var capturedYaw = lockedYaw;
        fixYawRenderer = new ActionRenderer(() => capturedEntity.Pos.Yaw = capturedYaw);
        capi.Event.RegisterRenderer(fixYawRenderer, EnumRenderStage.Before, "emote-fix-yaw");
    }

    void UnlockYaw()
    {
        yawLocked = false;

        if (entity is EntityPlayer ep)
        {
            ep.BodyYawLimits = null;
            ep.HeadYawLimits = null;
        }

        if (fixYawRenderer != null && api is ICoreClientAPI capi)
        {
            capi.Event.UnregisterRenderer(fixYawRenderer, EnumRenderStage.Before);
            fixYawRenderer = null;
        }
    }

    void StartAnimation(CustomEmote emote)
    {
        if (entity.Properties.Client?.AnimationsByMetaCode == null) return;
        if (!entity.Properties.Client.AnimationsByMetaCode.TryGetValue(emote.Animation, out var meta)) return;
        var clone = meta.Clone();
        clone.ClientSide = true;
        clone.EaseInSpeed = 999f;
        entity.AnimManager?.StartAnimation(clone);
    }

    public override void OnGameTick(float deltaTime)
    {
        if (api.Side == EnumAppSide.Client)
        {
            OnClientGameTick();
            return;
        }

        if (entity is not EntityPlayer player) return;
        var controls = player.ServerControls;
        if (controls == null) return;
        var motion = entity.ServerPos.Motion;
        bool moving = controls.TriesToMove || controls.Jump || motion.X * motion.X + motion.Z * motion.Z > 1e-5;
        if (!moving) return;

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

    void OnClientGameTick()
    {
        if (api is not ICoreClientAPI capi) return;
        if (capi.World.Player?.Entity?.EntityId != entity.EntityId) return;
        if (!yawLocked) return;

        var controls = (entity as EntityAgent)?.Controls;
        if (controls == null || (!controls.TriesToMove && !controls.Jump)) return;

        UnlockYaw();
        capi.ModLoader.GetModSystem<EmotesModSystem>().SendStopEmotes();
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

    public override void OnEntityDespawn(EntityDespawnData despawnData)
    {
        base.OnEntityDespawn(despawnData);
        UnlockYaw();
        if (entity.AnimManager != null)
            entity.AnimManager.OnAnimationStopped -= OnAnimationStopped;
    }

    class ActionRenderer : IRenderer
    {
        readonly System.Action action;
        public ActionRenderer(System.Action action) => this.action = action;
        public double RenderOrder => 0.5;
        public int RenderRange => 9999;
        public void OnRenderFrame(float deltaTime, EnumRenderStage stage) => action();
        public void Dispose() { }
    }
}
