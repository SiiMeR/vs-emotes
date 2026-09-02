using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Emotes;

public class BehaviorEmotes : EntityBehavior
{
    ICoreAPI api;
    EmotesModSystem modSystem;
    IRenderer fixYawRenderer;
    float lockedYaw;
    bool yawLocked;
    double originalEyeHeight;
    bool eyeHeightOverrideActive;
    Animation[] injectionSource;
    Animation[] injectionCache;

    public BehaviorEmotes(Entity entity) : base(entity) { }

    public override string PropertyName() => "emotes";

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        base.Initialize(properties, attributes);
        api = entity.Api;
        modSystem = api.ModLoader.GetModSystem<EmotesModSystem>();

        if (api.Side == EnumAppSide.Server && entity is EntityPlayer player)
        {
            EmoteState.StopAll(player);
            entity.WatchedAttributes.RegisterModifiedListener("mountedOn", OnMountChanged);
            entity.WatchedAttributes.RegisterModifiedListener("carrying", OnCarryChanged);
            entity.WatchedAttributes.RegisterModifiedListener("carried", OnCarryChanged);
            entity.WatchedAttributes.RegisterModifiedListener("emotes", OnEmotesChangedServer);
        }

        if (api.Side != EnumAppSide.Client) return;

        entity.WatchedAttributes.RegisterModifiedListener("emotes", SyncAnimations);

        if (entity.AnimManager != null)
            entity.AnimManager.OnAnimationStopped += OnAnimationStopped;

        SyncAnimations();
    }

    void OnMountChanged()
    {
        if (entity is not EntityPlayer player) return;
        if (!entity.WatchedAttributes.HasAttribute("mountedOn")) return;
        EmoteState.StopAll(player);
    }

    void OnCarryChanged()
    {
        if (entity is not EntityPlayer player) return;
        if (!EmoteState.InCarry(player)) return;
        EmoteState.StopAll(player);
    }

    void OnEmotesChangedServer()
    {
        if (entity is not EntityPlayer player) return;

        if (EmoteState.InCarry(player) && EmoteState.IsEmoting(player))
        {
            EmoteState.StopAll(player);
            return;
        }

        var tree = player.WatchedAttributes.GetTreeAttribute("emotes");

        if (string.IsNullOrEmpty(tree?.GetString("pairPartner"))) return;
        if (modSystem.Emotes.Any(kv => kv.Value.RequiresTarget && tree.GetBool(kv.Key))) return;
        modSystem?.TryEndPair(player);
    }

    void OnAnimationStopped(string animCode)
    {
        var capi = api as ICoreClientAPI;
        if (capi?.World.Player?.Entity?.EntityId != entity.EntityId) return;

        foreach (var (code, emote) in modSystem.Emotes)
        {
            if (emote.AnimationCode != animCode) continue;
            var tree = entity.WatchedAttributes.GetTreeAttribute("emotes");
            if (tree?.GetBool(code) != true) continue;

            if (emote.StopAfterAnimation)
                modSystem?.SendStopEmotes();
            break;
        }
    }

    void SyncAnimations()
    {
        var tree = entity.WatchedAttributes.GetTreeAttribute("emotes");
        var tpManager = (entity as EntityPlayer)?.TpAnimManager;

        bool anyActive = false;
        CustomEmote activeEmote = null;
        foreach (var (code, emote) in modSystem.Emotes)
        {
            bool shouldPlay = tree?.GetBool(code) ?? false;
            bool isPlaying = (tpManager ?? entity.AnimManager)?.IsAnimationActive(emote.AnimationCode) ?? false;

            if (shouldPlay && !isPlaying)
                StartAnimation(emote);
            else if (!shouldPlay && isPlaying)
                entity.AnimManager?.StopAnimation(emote.AnimationCode);

            if (shouldPlay) { anyActive = true; activeEmote = emote; }
        }

        if (anyActive && !yawLocked) LockYaw();
        else if (!anyActive && yawLocked) UnlockYaw();

        if (api is not ICoreClientAPI capi) return;
        if (capi.World.Player?.Entity?.EntityId != entity.EntityId) return;
        if (entity is not EntityPlayer ep) return;

        if (activeEmote?.EyeHeight != null)
            StartEyePos(ep, activeEmote.EyeHeight.Value);
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
                if (modSystem.Emotes.Any(kv => kv.Value.RequiresTarget && tree?.GetBool(kv.Key) == true))
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
        if (emote.Meta == null) return;
        entity.AnimManager?.StartAnimation(emote.Meta.Clone());
    }

    public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned,
        ref string[] willDeleteElements)
    {
        base.OnTesselation(ref entityShape, shapePathForLogging, ref shapeIsCloned, ref willDeleteElements);

        if (api == null || api.Side == EnumAppSide.Server) return;
        if (entityShape == null || modSystem == null) return;

        var available = modSystem.InjectedAnimations;
        if (available == null || available.Length == 0) return;

        try
        {
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var animation in entityShape.Animations ?? Array.Empty<Animation>())
                if (animation?.Code != null)
                    existing.Add(animation.Code);

            var missing = available.Where(a => a?.Code != null && !existing.Contains(a.Code)).ToArray();
            if (missing.Length == 0) return;

            if (!shapeIsCloned)
            {
                entityShape = entityShape.Clone();
                shapeIsCloned = true;
            }

            var added = BuildInjection(available, missing, entityShape, shapePathForLogging);
            entityShape.Animations = (entityShape.Animations ?? Array.Empty<Animation>()).Append(added);
        }
        catch (Exception e)
        {
            modSystem.Mod.Logger.Error("Failed to add emote animations to {0}: {1}", shapePathForLogging, e);
        }
    }

    Animation[] BuildInjection(Animation[] available, Animation[] missing, Shape entityShape,
        string shapePathForLogging)
    {
        if (injectionSource == available && injectionCache?.Length == missing.Length)
        {
            foreach (var cached in injectionCache)
                cached.PrevNextKeyFrameByFrame = null;
            return injectionCache;
        }

        var shapeVersion = entityShape.Animations is { Length: > 0 } ? entityShape.Animations[0].Version : 0;
        var elements = modSystem.MarkShapeValidated(shapePathForLogging) ? CollectElementNames(entityShape) : null;

        var added = new Animation[missing.Length];
        for (var i = 0; i < missing.Length; i++)
        {
            var clone = missing[i].Clone();
            if (clone.Version != shapeVersion)
            {
                WarnOnce(clone.Code, "version",
                    "Emote animation '{0}' has version {1} but the player shape uses version {2}, forcing {2}",
                    clone.Code, clone.Version, shapeVersion);
                clone.Version = shapeVersion;
            }

            if (elements != null)
                WarnUnknownElements(clone, elements);

            added[i] = clone;
        }

        injectionSource = available;
        injectionCache = added;
        return added;
    }

    static HashSet<string> CollectElementNames(Shape shape)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Walk(ShapeElement[] children)
        {
            foreach (var element in children ?? Array.Empty<ShapeElement>())
            {
                if (element?.Name == null) continue;
                names.Add(element.Name);
                Walk(element.Children);
            }
        }

        Walk(shape.Elements);
        return names;
    }

    void WarnUnknownElements(Animation animation, HashSet<string> shapeElements)
    {
        if (shapeElements.Count == 0) return;

        var unknown = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var frame in animation.KeyFrames ?? Array.Empty<AnimationKeyFrame>())
        foreach (var name in frame?.Elements?.Keys ?? Enumerable.Empty<string>())
            if (!shapeElements.Contains(name))
                unknown.Add(name);

        if (unknown.Count == 0) return;

        WarnOnce(animation.Code, "elements",
            "Emote animation '{0}' animates elements the player shape does not have, they will be ignored: {1}",
            animation.Code, string.Join(", ", unknown));
    }

    void WarnOnce(string animationCode, string kind, string message, params object[] args)
    {
        if (!modSystem.MarkWarned(animationCode + "/" + kind)) return;
        modSystem.Mod.Logger.Warning(message, args);
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
        foreach (var (code, emote) in modSystem.Emotes)
        {
            bool shouldStop = controls.FloorSitting || (moving && emote.StopOnMovement);
            if (!shouldStop || !tree.GetBool(code)) continue;
            tree.SetBool(code, false);
            anyChanged = true;
        }

        if (anyChanged)
            EmoteState.MarkDirty(player);
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
        modSystem?.SendStopEmotes();
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
        foreach (var (code, emote) in modSystem.Emotes)
        {
            if (!emote.StopOnDamage || !tree.GetBool(code)) continue;
            tree.SetBool(code, false);
            anyChanged = true;
        }

        if (anyChanged)
            EmoteState.MarkDirty(player);
    }

    public override void OnEntityDeath(DamageSource damageSource)
    {
        base.OnEntityDeath(damageSource);

        if (api.Side != EnumAppSide.Server) return;
        if (entity is not EntityPlayer player) return;
        EmoteState.StopAll(player);
    }

    public override void OnEntityDespawn(EntityDespawnData despawnData)
    {
        base.OnEntityDespawn(despawnData);
        if (api.Side == EnumAppSide.Server && entity is EntityPlayer player)
            modSystem?.TryEndPair(player);
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
