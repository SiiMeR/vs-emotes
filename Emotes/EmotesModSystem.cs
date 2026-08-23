using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Emotes;

public class EmotesModSystem : ModSystem
{
    private const string ChannelName = "emotes";
    private const string ConsentKey = "emotesConsent";

    private static readonly (BlockFacing facing, float yaw)[] HorizontalFacings =
    {
        (BlockFacing.NORTH, 0f),
        (BlockFacing.SOUTH, (float)Math.PI),
        (BlockFacing.EAST, -(float)(Math.PI / 2)),
        (BlockFacing.WEST, (float)(Math.PI / 2))
    };

    private readonly Dictionary<string, PairRequest> pairRequests = new();
    private HashSet<string> disabledEmotes = new();
    private HashSet<string> clientDisabledEmotes = new();
    private ICoreClientAPI clientApi;

    private IClientNetworkChannel clientChannel;

    private EmotesConfig config;
    private ICoreServerAPI serverApi;
    private IServerNetworkChannel serverChannel;

    private IReadOnlyDictionary<string, CustomEmote> emotes =
        new Dictionary<string, CustomEmote>(StringComparer.OrdinalIgnoreCase);

    private Animation[] injectedAnimations = Array.Empty<Animation>();

    private readonly HashSet<string> warned = new();

    public IReadOnlyDictionary<string, CustomEmote> Emotes => emotes;

    public bool MarkWarned(string key)
    {
        lock (warned)
        {
            return warned.Add(key);
        }
    }

    public bool MarkShapeValidated(string shapePath)
    {
        return MarkWarned("shape/" + shapePath);
    }

    public Animation[] InjectedAnimations => injectedAnimations;

    public override void AssetsLoaded(ICoreAPI api)
    {
        base.AssetsLoaded(api);

        var result = EmoteLoader.Load(api, Mod.Logger);
        emotes = result.Emotes;
        injectedAnimations = result.InjectedAnimations;

        Mod.Logger.Notification("Loaded {0} emotes from {1} files. Skipped: {2}. Animations injected: {3}",
            result.Emotes.Count, result.FileCount, result.SkippedCount, result.InjectedAnimations.Length);
    }

    public string GetEmoteName(CustomEmote emote)
    {
        if (emote == null) return "";
        return ResolveEmoteName(emote) ?? Capitalize(emote.Code);
    }

    private static string ResolveEmoteName(CustomEmote emote)
    {
        if (emote == null) return null;

        if (!string.IsNullOrEmpty(emote.Name))
        {
            if (!emote.Name.Contains(':')) return emote.Name;
            if (Lang.HasTranslation(emote.Name)) return Lang.Get(emote.Name);
        }

        var domainKey = emote.Domain + ":emote-" + emote.Code;
        if (Lang.HasTranslation(domainKey)) return Lang.Get(domainKey);

        var fallbackKey = "emotes:emote-" + emote.Code;
        if (Lang.HasTranslation(fallbackKey)) return Lang.Get(fallbackKey);

        return null;
    }

    public string GetEmoteName(string code)
    {
        if (emotes.TryGetValue(code, out var emote)) return GetEmoteName(emote);

        var key = "emotes:emote-" + code;
        return Lang.HasTranslation(key) ? Lang.Get(key) : Capitalize(code);
    }

    private void ReportMissingTranslations()
    {
        foreach (var emote in emotes.Values)
        {
            if (ResolveEmoteName(emote) != null) continue;
            Mod.Logger.Notification("Emote '{0}' from {1} has no display name, add lang key \"{2}\"",
                emote.Code, emote.Source, emote.Domain + ":emote-" + emote.Code);
        }

        foreach (var group in emotes.Values.GroupBy(e => e.Category))
        {
            if (ResolveCategoryName(group.Key) != null) continue;
            Mod.Logger.Notification("Category '{0}' has no display name, add lang key \"{1}\"",
                group.Key, group.First().Domain + ":cat-" + group.Key);
        }
    }

    public string GetCategoryName(string category)
    {
        if (string.IsNullOrEmpty(category)) return "";
        return ResolveCategoryName(category) ?? Capitalize(category);
    }

    private string ResolveCategoryName(string category)
    {
        if (string.IsNullOrEmpty(category)) return null;

        var members = emotes.Values.Where(e => e.Category == category).ToArray();

        var declared = members.Select(e => e.CategoryName)
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal);

        foreach (var name in declared)
        {
            if (!name.Contains(':')) return name;
            if (Lang.HasTranslation(name)) return Lang.Get(name);
        }

        var ownKey = "emotes:cat-" + category;
        if (Lang.HasTranslation(ownKey)) return Lang.Get(ownKey);

        var domains = members.Select(e => e.Domain)
            .Where(d => !string.IsNullOrEmpty(d))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(d => d, StringComparer.Ordinal);

        foreach (var domain in domains)
        {
            var key = domain + ":cat-" + category;
            if (Lang.HasTranslation(key)) return Lang.Get(key);
        }

        return null;
    }

    public static string Capitalize(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return char.ToUpperInvariant(text[0]) + text.Substring(1);
    }

    public bool IsEmoteDisabled(string code)
    {
        return clientDisabledEmotes.Contains(code);
    }

    public static bool IsEntityEmoting(Entity entity)
    {
        var tree = entity?.WatchedAttributes?.GetTreeAttribute("emotes");
        if (tree == null)
        {
            return false;
        }

        foreach (var attribute in tree)
        {
            if (attribute.Value is BoolAttribute { value: true })
            {
                return true;
            }
        }

        return false;
    }

    private static void ClearEmoteBools(ITreeAttribute tree)
    {
        if (tree == null)
        {
            return;
        }

        foreach (var attribute in tree)
        {
            if (attribute.Value is BoolAttribute boolAttribute)
            {
                boolAttribute.value = false;
            }
        }
    }

    public static void SetEmoteState(EntityPlayer player, string code, bool active)
    {
        var tree = player.WatchedAttributes.GetOrAddTreeAttribute("emotes");
        tree.SetBool(code, active);
        player.WatchedAttributes.MarkPathDirty("emotes");
    }

    public static void StopAllEmotes(EntityPlayer player)
    {
        var tree = player.WatchedAttributes.GetOrAddTreeAttribute("emotes");
        ClearEmoteBools(tree);
        tree.RemoveAttribute("leanYaw");
        player.WatchedAttributes.MarkPathDirty("emotes");
    }

    public void SendToggleEmote(string code)
    {
        if (clientApi?.World.Player?.Entity?.MountedOn != null)
        {
            clientApi.TriggerIngameError(this, "emote-mounted", Lang.Get("emotes:cmd-mounted"));
            return;
        }

        clientChannel?.SendPacket(new EmotePacket { Code = code });
    }

    public void SendStopEmotes()
    {
        clientChannel?.SendPacket(new EmotePacket { ForceStop = true });
    }

    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        api.RegisterEntityBehaviorClass("emotes", typeof(BehaviorEmotes));
        api.Event.OnEntitySpawn += OnEntitySpawn;
    }

    public override void Dispose()
    {
        if (clientApi?.ModLoader.IsModEnabled("overhaullib") == true)
        {
            CombatOverhaulPatch.Remove();
        }

        base.Dispose();
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);
        clientApi = api;
        if (api.ModLoader.IsModEnabled("overhaullib"))
        {
            CombatOverhaulPatch.Apply(api);
        }

        clientChannel = api.Network.RegisterChannel(ChannelName)
            .RegisterMessageType<EmotePacket>()
            .RegisterMessageType<LeanSnapPacket>()
            .RegisterMessageType<DisabledEmotesPacket>()
            .SetMessageHandler<LeanSnapPacket>(OnLeanSnap)
            .SetMessageHandler<DisabledEmotesPacket>(OnDisabledEmotes);

        ReportMissingTranslations();

        api.Input.RegisterHotKey("emotepicker", Lang.Get("emotes:hotkey-open"), GlKeys.J, shiftPressed: true);
        var dialog = new GuiDialogEmotePicker(api, this);
        api.Input.SetHotKeyHandler("emotepicker", _ =>
        {
            if (dialog.IsOpened())
            {
                dialog.TryClose();
            }
            else
            {
                dialog.TryOpen();
            }

            return true;
        });
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);
        serverApi = api;
        config = api.LoadModConfig<EmotesConfig>("emotes.json") ?? new EmotesConfig();
        api.StoreModConfig(config, "emotes.json");
        disabledEmotes = new HashSet<string>(config.DisabledEmotes ?? new List<string>(),
            StringComparer.OrdinalIgnoreCase);

        serverChannel = api.Network.RegisterChannel(ChannelName)
            .RegisterMessageType<EmotePacket>()
            .RegisterMessageType<LeanSnapPacket>()
            .RegisterMessageType<DisabledEmotesPacket>()
            .SetMessageHandler<EmotePacket>(OnEmotePacket);

        api.Event.PlayerJoin += SendDisabledEmotes;

        var cmd = api.ChatCommands
            .GetOrCreate("emotes")
            .RequiresPrivilege(Privilege.chat)
            .WithDescription(Lang.Get("emotes:cmd-description"));

        cmd.BeginSubCommand("play")
            .RequiresPlayer()
            .WithArgs(api.ChatCommands.Parsers.Word("name"))
            .HandleWith(HandlePlayCommand)
            .EndSubCommand();

        cmd.BeginSubCommand("list")
            .HandleWith(HandleListCommand)
            .EndSubCommand();

        cmd.BeginSubCommand("stop")
            .RequiresPlayer()
            .HandleWith(HandleStopCommand)
            .EndSubCommand();

        cmd.BeginSubCommand("showcase")
            .RequiresPlayer()
            .HandleWith(HandleShowcaseCommand)
            .EndSubCommand();

        cmd.BeginSubCommand("consent")
            .RequiresPlayer()
            .WithArgs(api.ChatCommands.Parsers.Bool("autoaccept"))
            .HandleWith(HandleConsentCommand)
            .EndSubCommand();

        cmd.BeginSubCommand("accept")
            .RequiresPlayer()
            .HandleWith(OnPairAccepted)
            .EndSubCommand();

        cmd.BeginSubCommand("refuse")
            .RequiresPlayer()
            .HandleWith(OnPairRefused)
            .EndSubCommand();
    }

    private void OnEmotePacket(IServerPlayer fromPlayer, EmotePacket packet)
    {
        if (fromPlayer.Entity is not EntityPlayer player)
        {
            return;
        }

        if (packet.ForceStop)
        {
            StopAllEmotes(player);
            return;
        }

        if (!Emotes.TryGetValue(packet.Code, out var emoteInfo))
        {
            return;
        }

        if (disabledEmotes.Contains(packet.Code))
        {
            return;
        }

        if (emoteInfo.RequiresTarget)
        {
            OnPairInitiate(packet.Code,
                new TextCommandCallingArgs { Caller = new Caller { Player = fromPlayer, Entity = fromPlayer.Entity } });
            return;
        }

        if (player.MountedOn != null)
        {
            return;
        }

        var tree = player.WatchedAttributes.GetOrAddTreeAttribute("emotes");
        var isActive = tree.GetBool(packet.Code);

        if (!isActive && emoteInfo.SnapToWall)
        {
            if (!TrySnapToWall(fromPlayer))
            {
                tree.RemoveAttribute("leanYaw");
            }
        }
        else
        {
            tree.RemoveAttribute("leanYaw");
        }

        ClearEmoteBools(tree);

        if (!isActive)
        {
            tree.SetBool(packet.Code, true);
        }

        player.WatchedAttributes.MarkPathDirty("emotes");
    }

    private void SendDisabledEmotes(IServerPlayer player)
    {
        serverChannel?.SendPacket(new DisabledEmotesPacket { Codes = disabledEmotes.ToArray() }, player);
    }

    private void OnDisabledEmotes(DisabledEmotesPacket packet)
    {
        clientDisabledEmotes = new HashSet<string>(packet.Codes ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

        injectedAnimations = emotes.Values
            .Where(e => e.InjectedAnimation != null && !clientDisabledEmotes.Contains(e.Code))
            .Select(e => e.InjectedAnimation)
            .ToArray();
    }

    private void OnLeanSnap(LeanSnapPacket packet)
    {
        if (clientApi.World.Player?.Entity is not EntityPlayer ep)
        {
            return;
        }

        ep.BodyYawLimits = new AngleConstraint(packet.Yaw, 0f);
    }

    private bool TrySnapToWall(IServerPlayer player)
    {
        var entity = player.Entity;
        var world = entity.World;
        var pos = entity.Pos;
        var blockPos = pos.AsBlockPos;

        foreach (var (facing, yaw) in HorizontalFacings)
        {
            var norm = facing.Normali;
            BlockPos wallPos = null;
            for (var dist = 1; dist <= 2; dist++)
            {
                var candidate = blockPos.AddCopy(norm.X * dist, 0, norm.Z * dist);
                if (IsSolidWall(world, candidate, facing))
                {
                    wallPos = candidate;
                    break;
                }
            }

            if (wallPos == null)
            {
                continue;
            }

            var gap = entity.Properties.CollisionBoxSize?.X / 2.0 ?? 0.3;
            double snapX = pos.X, snapZ = pos.Z;

            if (norm.X != 0)
            {
                snapX = wallPos.X + (norm.X > 0 ? -gap : 1.0 + gap);
            }
            else
            {
                snapZ = wallPos.Z + (norm.Z > 0 ? -gap : 1.0 + gap);
            }

            entity.TeleportToDouble(snapX, pos.Y, snapZ);
            entity.Pos.Yaw = yaw;
            entity.WatchedAttributes.GetOrAddTreeAttribute("emotes").SetFloat("leanYaw", yaw);
            serverChannel.SendPacket(new LeanSnapPacket { Yaw = yaw }, player);
            return true;
        }

        return false;
    }

    private static bool IsSolidWall(IWorldAccessor world, BlockPos pos, BlockFacing facingToWall)
    {
        var playerSide = facingToWall.Opposite;
        var low = world.BlockAccessor.GetBlock(pos);
        var high = world.BlockAccessor.GetBlock(pos.AddCopy(0, 1, 0));
        return low.SideSolid[playerSide.Index] && high.SideSolid[playerSide.Index];
    }

    private void OnEntitySpawn(Entity entity)
    {
        if (entity is not EntityPlayer)
        {
            return;
        }

        if (entity.GetBehavior<BehaviorEmotes>() != null)
        {
            return;
        }

        var behavior = new BehaviorEmotes(entity);
        entity.AddBehavior(behavior);
        behavior.Initialize(entity.Properties, null);
    }

    private float? SnapPairPositions(IServerPlayer initiatorPlayer, EntityPlayer initiator, Entity target,
        float pairDistance)
    {
        var dx = target.Pos.X - initiator.Pos.X;
        var dz = target.Pos.Z - initiator.Pos.Z;
        var dist = Math.Sqrt(dx * dx + dz * dz);

        if (dist > 3.0 || dist < 0.01)
        {
            return null;
        }

        var midX = (initiator.Pos.X + target.Pos.X) / 2;
        var midZ = (initiator.Pos.Z + target.Pos.Z) / 2;
        var normX = dx / dist;
        var normZ = dz / dist;

        initiator.TeleportToDouble(midX - normX * pairDistance, initiator.Pos.Y, midZ - normZ * pairDistance);
        target.TeleportToDouble(midX + normX * pairDistance, initiator.Pos.Y, midZ + normZ * pairDistance);

        var yaw = (float)Math.Atan2(dx, dz);
        initiator.Pos.Yaw = yaw;
        target.Pos.Yaw = yaw + (float)Math.PI;

        serverChannel.SendPacket(new LeanSnapPacket { Yaw = yaw }, initiatorPlayer);
        return yaw;
    }

    private TextCommandResult OnPairInitiate(string emoteCode, TextCommandCallingArgs args)
    {
        if (args.Caller.Entity is not EntityPlayer initiator)
        {
            return TextCommandResult.Error(Lang.Get("emotes:cmd-only-players"));
        }

        var initiatorPlayer = (IServerPlayer)args.Caller.Player;
        var emote = Emotes[emoteCode];
        var selected = initiator.EntitySelection?.Entity;

        if (selected == null)
        {
            return TextCommandResult.Error(Lang.Get("emotes:pair-no-target"));
        }

        if (selected is EntityPlayerBot bot)
        {
            var botYaw = SnapPairPositions(initiatorPlayer, initiator, bot, emote.PairDistance);
            if (botYaw == null)
            {
                return TextCommandResult.Error(Lang.Get("emotes:pair-too-far"));
            }

            StopAllEmotes(initiator);
            var botTree = initiator.WatchedAttributes.GetOrAddTreeAttribute("emotes");
            botTree.SetFloat("pairYaw", botYaw.Value);
            SetEmoteState(initiator, emoteCode, true);
            return TextCommandResult.Success();
        }

        if (selected is not EntityPlayer target)
        {
            return TextCommandResult.Error(Lang.Get("emotes:pair-no-target"));
        }

        if (target.EntityId == initiator.EntityId)
        {
            return TextCommandResult.Error(Lang.Get("emotes:pair-self"));
        }

        if (initiator.Pos.DistanceTo(target.Pos) > 3.0)
        {
            return TextCommandResult.Error(Lang.Get("emotes:pair-too-far"));
        }

        if (target.Player is not IServerPlayer targetPlayer)
        {
            return TextCommandResult.Error(Lang.Get("emotes:pair-no-target"));
        }

        if (!targetPlayer.GetModData(ConsentKey, config.PairedEmotesRequireConsent))
        {
            return ExecutePair(emoteCode, emote, initiatorPlayer, initiator, targetPlayer, target);
        }

        pairRequests[initiatorPlayer.PlayerUID] = new PairRequest(initiatorPlayer.PlayerUID, targetPlayer.PlayerUID,
            emoteCode, DateTime.Now);

        var accept = "<a href=\"command:///emotes accept\">Accept</a>";
        var refuse = "<a href=\"command:///emotes refuse\">Refuse</a>";
        targetPlayer.SendMessage(GlobalConstants.CurrentChatGroup,
            Lang.Get("emotes:pair-request-received", initiator.GetName(), GetEmoteName(emoteCode), accept,
                refuse),
            EnumChatType.GroupInvite);

        return TextCommandResult.Success(Lang.Get("emotes:pair-request-sent", target.GetName()));
    }

    private TextCommandResult ExecutePair(string emoteCode, CustomEmote emote, IServerPlayer initiatorPlayer,
        EntityPlayer initiatorEntity, IServerPlayer targetPlayer, EntityPlayer targetEntity)
    {
        var yaw = SnapPairPositions(initiatorPlayer, initiatorEntity, targetEntity, emote.PairDistance);
        if (yaw == null)
        {
            return TextCommandResult.Error(Lang.Get("emotes:pair-too-far"));
        }

        var targetYaw = yaw.Value + (float)Math.PI;
        serverChannel.SendPacket(new LeanSnapPacket { Yaw = targetYaw }, targetPlayer);

        StopAllEmotes(initiatorEntity);
        StopAllEmotes(targetEntity);

        var initiatorTree = initiatorEntity.WatchedAttributes.GetOrAddTreeAttribute("emotes");
        initiatorTree.SetString("pairPartner", targetPlayer.PlayerUID);
        initiatorTree.SetFloat("pairYaw", yaw.Value);

        var targetTree = targetEntity.WatchedAttributes.GetOrAddTreeAttribute("emotes");
        targetTree.SetString("pairPartner", initiatorPlayer.PlayerUID);
        targetTree.SetFloat("pairYaw", targetYaw);

        SetEmoteState(initiatorEntity, emoteCode, true);
        SetEmoteState(targetEntity, emoteCode, true);

        return TextCommandResult.Success();
    }

    private TextCommandResult HandleConsentCommand(TextCommandCallingArgs args)
    {
        if (args.Caller.Player is not IServerPlayer player)
        {
            return TextCommandResult.Error(Lang.Get("emotes:cmd-only-players"));
        }

        var requireConsent = (bool)args[0];
        player.SetModData(ConsentKey, requireConsent);
        return TextCommandResult.Success(Lang.Get(requireConsent ? "emotes:consent-on" : "emotes:consent-off"));
    }

    private TextCommandResult OnPairAccepted(TextCommandCallingArgs args)
    {
        var callerPlayer = (IServerPlayer)args.Caller.Player;
        var request = pairRequests.Values
            .OrderByDescending(r => r.RequestTime)
            .FirstOrDefault(r => r.TargetUid == callerPlayer.PlayerUID);

        if (request == null)
        {
            return TextCommandResult.Error(Lang.Get("emotes:pair-no-request"));
        }

        if (serverApi.World.PlayerByUid(request.InitiatorUid) is not IServerPlayer initiatorPlayer)
        {
            pairRequests.Remove(request.InitiatorUid);
            return TextCommandResult.Error(Lang.Get("emotes:pair-initiator-gone"));
        }

        pairRequests.Remove(request.InitiatorUid);

        if (initiatorPlayer.Entity is not EntityPlayer initiatorEntity ||
            callerPlayer.Entity is not EntityPlayer targetEntity)
        {
            return TextCommandResult.Error(Lang.Get("emotes:pair-no-target"));
        }

        return ExecutePair(request.EmoteCode, Emotes[request.EmoteCode], initiatorPlayer, initiatorEntity, callerPlayer,
            targetEntity);
    }

    private TextCommandResult OnPairRefused(TextCommandCallingArgs args)
    {
        var callerPlayer = (IServerPlayer)args.Caller.Player;
        var request = pairRequests.Values
            .OrderByDescending(r => r.RequestTime)
            .FirstOrDefault(r => r.TargetUid == callerPlayer.PlayerUID);

        if (request == null)
        {
            return TextCommandResult.Error(Lang.Get("emotes:pair-no-request"));
        }

        pairRequests.Remove(request.InitiatorUid);

        if (serverApi.World.PlayerByUid(request.InitiatorUid) is IServerPlayer initiatorPlayer)
        {
            initiatorPlayer.SendMessage(GlobalConstants.CurrentChatGroup,
                Lang.Get("emotes:pair-refused", callerPlayer.Entity?.GetName() ?? callerPlayer.PlayerName),
                EnumChatType.Notification);
        }

        return TextCommandResult.Success();
    }

    public void TryEndPair(EntityPlayer player)
    {
        var tree = player.WatchedAttributes.GetTreeAttribute("emotes");
        var partnerUid = tree?.GetString("pairPartner");
        if (string.IsNullOrEmpty(partnerUid))
        {
            return;
        }

        tree.SetString("pairPartner", "");
        player.WatchedAttributes.MarkPathDirty("emotes");

        if (serverApi.World.PlayerByUid(partnerUid) is not IServerPlayer partnerPlayer)
        {
            return;
        }

        if (partnerPlayer.Entity is not EntityPlayer partnerEntity)
        {
            return;
        }

        partnerEntity.WatchedAttributes.GetOrAddTreeAttribute("emotes").SetString("pairPartner", "");
        StopAllEmotes(partnerEntity);
    }

    private TextCommandResult HandlePlayCommand(TextCommandCallingArgs args)
    {
        if (args.Caller.Entity is not EntityPlayer player)
        {
            return TextCommandResult.Error(Lang.Get("emotes:cmd-only-players"));
        }

        if (player.MountedOn != null)
        {
            return TextCommandResult.Error(Lang.Get("emotes:cmd-mounted"));
        }

        var key = ((string)args[0]).ToLowerInvariant();
        if (!Emotes.TryGetValue(key, out var emote))
        {
            return TextCommandResult.Error(Lang.Get("emotes:cmd-not-found", key));
        }

        if (disabledEmotes.Contains(key))
        {
            return TextCommandResult.Error(Lang.Get("emotes:cmd-disabled", key));
        }

        if (emote.RequiresTarget)
        {
            return OnPairInitiate(key, args);
        }

        var tree = player.WatchedAttributes.GetOrAddTreeAttribute("emotes");
        var isActive = tree.GetBool(emote.Code);
        ClearEmoteBools(tree);

        if (!isActive)
        {
            tree.SetBool(emote.Code, true);
        }

        player.WatchedAttributes.MarkPathDirty("emotes");

        var displayName = GetEmoteName(emote);
        return TextCommandResult.Success(isActive
            ? Lang.Get("emotes:cmd-emote-stopped", displayName)
            : Lang.Get("emotes:cmd-emote-started", displayName));
    }

    private TextCommandResult HandleListCommand(TextCommandCallingArgs args)
    {
        var solo = string.Join(", ",
            Emotes.Values.Where(e => !e.RequiresTarget && !disabledEmotes.Contains(e.Code))
                .Select(e => $"{e.Code} ({GetEmoteName(e)})"));
        var paired = string.Join(", ",
            Emotes.Values.Where(e => e.RequiresTarget && !disabledEmotes.Contains(e.Code))
                .Select(e => $"{e.Code} ({GetEmoteName(e)})"));

        var message = Lang.Get("emotes:cmd-available-emotes", solo);
        if (!string.IsNullOrEmpty(paired))
        {
            message += "\n" + Lang.Get("emotes:cmd-paired-emotes", paired);
        }

        return TextCommandResult.Success(message);
    }

    private TextCommandResult HandleStopCommand(TextCommandCallingArgs args)
    {
        if (args.Caller.Entity is not EntityPlayer player)
        {
            return TextCommandResult.Error(Lang.Get("emotes:cmd-only-players"));
        }

        StopAllEmotes(player);
        return TextCommandResult.Success(Lang.Get("emotes:cmd-all-stopped"));
    }

    private TextCommandResult HandleShowcaseCommand(TextCommandCallingArgs args)
    {
        if (args.Caller.Entity is not EntityPlayer player)
        {
            return TextCommandResult.Error(Lang.Get("emotes:cmd-only-players"));
        }

        var codes = Emotes.Keys.Where(c => !disabledEmotes.Contains(c)).ToList();
        var cancelled = new bool[1];
        var tickId = new long[1];

        tickId[0] = serverApi.Event.RegisterGameTickListener(_ =>
        {
            if (cancelled[0])
            {
                serverApi.Event.UnregisterGameTickListener(tickId[0]);
                return;
            }

            var controls = player.ServerControls;
            if (!controls.TriesToMove && !controls.Jump)
            {
                return;
            }

            cancelled[0] = true;
            serverApi.Event.UnregisterGameTickListener(tickId[0]);
            StopAllEmotes(player);
        }, 100);

        void RunNext(int index)
        {
            if (!player.Alive || cancelled[0])
            {
                return;
            }

            if (index >= codes.Count)
            {
                StopAllEmotes(player);
                serverApi.Event.UnregisterGameTickListener(tickId[0]);
                return;
            }

            var t = player.WatchedAttributes.GetOrAddTreeAttribute("emotes");
            ClearEmoteBools(t);
            t.SetBool(codes[index], true);
            player.WatchedAttributes.MarkPathDirty("emotes");
            serverApi.Event.RegisterCallback(_ =>
            {
                if (!player.Alive || cancelled[0])
                {
                    return;
                }

                StopAllEmotes(player);
                serverApi.Event.RegisterCallback(_ => RunNext(index + 1), 1000);
            }, 4000);
        }

        serverApi.Event.RegisterCallback(_ => RunNext(0), 3000);
        return TextCommandResult.Success(Lang.Get("emotes:cmd-showcase-start"));
    }

    private record PairRequest(string InitiatorUid, string TargetUid, string EmoteCode, DateTime RequestTime);
}