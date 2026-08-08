using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Emotes;

public class BehaviorEmotes : EntityBehavior
{
    static readonly Dictionary<string, double> EyeHeightByEmote = new()
    {
        ["seiza"]          = 0.97,
        ["kneel"]          = 1.09,
        ["prayer"]         = 0.97,
        ["sittingcool"]    = 0.89,
        ["sittingchill"]   = 0.89,
        ["sittingrelaxed"] = 0.89,
        ["sittingrefined"] = 0.89,
        ["sittingcalm"]    = 0.89,
        ["sittinginnocent"]= 0.89,
        ["sittingrested"]  = 0.89,
        ["sittingintrovert"]= 0.89,
        ["squatting"]      = 0.95,
        ["layingback"]     = 0.2,
        ["layingdown"]     = 0.2,
        ["laydownsensual"] = 0.2,
        ["prone"]          = 0.2,
        ["playdead"]       = 0.2,
    };

    ICoreAPI api;
    IRenderer fixYawRenderer;
    float lockedYaw;
    bool yawLocked;
    double originalEyeHeight;
    bool eyeHeightOverrideActive;

    public BehaviorEmotes(Entity entity) : base(entity) { }

    public override string PropertyName() => "emotes";

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        base.Initialize(properties, attributes);
        api = entity.Api;

        if (api.Side == EnumAppSide.Server && entity is EntityPlayer player)
        {
            EmotesModSystem.StopAllEmotes(player);
            entity.WatchedAttributes.RegisterModifiedListener("mountedOn", OnMountChanged);
            entity.WatchedAttributes.RegisterModifiedListener("emotes", OnEmotesChangedServer);
        }

        if (api.Side != EnumAppSide.Client) return;

        entity.WatchedAttributes.RegisterModifiedListener("emotes", SyncAnimations);

        if (entity.AnimManager != null)
            entity.AnimManager.OnAnimationStopped += OnAnimationStopped;
    }

    void OnMountChanged()
    {
        if (entity is not EntityPlayer player) return;
        if (!entity.WatchedAttributes.HasAttribute("mountedOn")) return;
        EmotesModSystem.StopAllEmotes(player);
    }

    void OnEmotesChangedServer()
    {
        if (entity is not EntityPlayer player) return;
        var tree = player.WatchedAttributes.GetTreeAttribute("emotes");

        if (string.IsNullOrEmpty(tree?.GetString("pairPartner"))) return;
        if (EmotesModSystem.GetEmotes().Any(kv => kv.Value.RequiresTarget && tree.GetBool(kv.Key))) return;
        api.ModLoader.GetModSystem<EmotesModSystem>()?.TryEndPair(player);
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
                api.ModLoader.GetModSystem<EmotesModSystem>().SendStopEmotes();
            break;
        }
    }

    void SyncAnimations()
    {
        var tree = entity.WatchedAttributes.GetTreeAttribute("emotes");
        var tpManager = (entity as EntityPlayer)?.TpAnimManager;

        bool anyActive = false;
        string activeCode = null;
        foreach (var (code, emote) in EmotesModSystem.GetEmotes())
        {
            bool shouldPlay = tree?.GetBool(code) ?? false;
            bool isPlaying = (tpManager ?? entity.AnimManager)?.IsAnimationActive(emote.Animation) ?? false;

            if (shouldPlay && !isPlaying)
                StartAnimation(emote);
            else if (!shouldPlay && isPlaying)
                entity.AnimManager?.StopAnimation(emote.Animation);

            if (shouldPlay) { anyActive = true; activeCode = code; }
        }

        if (anyActive && !yawLocked) LockYaw();
        else if (!anyActive && yawLocked) UnlockYaw();

        if (api is not ICoreClientAPI capi) return;
        if (capi.World.Player?.Entity?.EntityId != entity.EntityId) return;
        if (entity is not EntityPlayer ep) return;

        if (activeCode != null && EyeHeightByEmote.TryGetValue(activeCode, out var eyeH))
            StartEyePos(ep, eyeH);
        else
            StopEyePos(ep);
    }

    void StartEyePos(EntityPlayer ep, double target)
    {
        if (!eyeHeightOverrideActive)
        {
            originalEyeHeight = ep.Properties.EyeHeight;
            eyeHeightOverrideActive = true;
        }
        ep.Properties.EyeHeight = target;
    }

    void StopEyePos(EntityPlayer ep)
    {
        if (!eyeHeightOverrideActive) return;
        ep.Properties.EyeHeight = originalEyeHeight;
        eyeHeightOverrideActive = false;
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
                var tree = entity.WatchedAttributes.GetTreeAttribute("emotes");
                if (EmotesModSystem.GetEmotes().Any(kv => kv.Value.RequiresTarget && tree?.GetBool(kv.Key) == true))
                    lockedYaw = tree.GetFloat("pairYaw");
                else if (tree != null && tree.HasAttribute("leanYaw"))
                    lockedYaw = tree.GetFloat("leanYaw");
                else
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
        fixYawRenderer = new ActionRenderer(_ => capturedEntity.Pos.Yaw = capturedYaw);
        capi.Event.RegisterRenderer(fixYawRenderer, EnumRenderStage.Before, "emote-fix-yaw");
    }

    void UnlockYaw()
    {
        yawLocked = false;

        var ep = entity as EntityPlayer;
        if (ep != null)
        {
            ep.BodyYawLimits = null;
            ep.HeadYawLimits = null;
        }

        if (api is not ICoreClientAPI capi) return;

        if (fixYawRenderer != null)
        {
            capi.Event.UnregisterRenderer(fixYawRenderer, EnumRenderStage.Before);
            fixYawRenderer = null;
        }

        if (ep != null)
            StopEyePos(ep);
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
        bool moving = player.Swimming || controls.TriesToMove || controls.Jump || controls.Sneak || controls.LeftMouseDown;
        if (!moving && !controls.FloorSitting) return;

        var tree = player.WatchedAttributes.GetTreeAttribute("emotes");
        if (tree == null) return;

        bool anyChanged = false;
        foreach (var (code, emote) in EmotesModSystem.GetEmotes())
        {
            bool shouldStop = controls.FloorSitting || (moving && emote.StopOnMovement);
            if (!shouldStop || !tree.GetBool(code)) continue;
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

        if (entity.Swimming || entity.FeetInLiquid)
        {
            entity.AnimManager?.StopAnimation("swimidle");
            entity.AnimManager?.StopAnimation("swim");
            return;
        }

        var controls = (entity as EntityAgent)?.Controls;
        if (controls == null || (!controls.TriesToMove && !controls.Jump && !controls.Sneak && !controls.LeftMouseDown)) return;

        UnlockYaw();
        capi.ModLoader.GetModSystem<EmotesModSystem>().SendStopEmotes();
    }

    public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
    {
        base.OnEntityReceiveDamage(damageSource, ref damage);

        if (api.Side != EnumAppSide.Server) return;
        if (damageSource.Type == EnumDamageType.Heal) return;
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
        if (api.Side == EnumAppSide.Server && entity is EntityPlayer player)
            api.ModLoader.GetModSystem<EmotesModSystem>()?.TryEndPair(player);
        UnlockYaw();
        if (entity.AnimManager != null)
            entity.AnimManager.OnAnimationStopped -= OnAnimationStopped;
    }

    class ActionRenderer : IRenderer
    {
        readonly Action<float> action;
        public ActionRenderer(Action<float> action) => this.action = action;
        public double RenderOrder => 0.5;
        public int RenderRange => 9999;
        public void OnRenderFrame(float deltaTime, EnumRenderStage stage) => action(deltaTime);
        public void Dispose() { }
    }
}
