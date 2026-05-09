using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Emotes;

public class EmotesModSystem : ModSystem
{
    private const string ChannelName = "emotes";

    private static readonly Dictionary<string, CustomEmote> Emotes = new()
    {
        ["seiza"] = new CustomEmote
        {
            Code = "seiza", Name = "Seiza", DisplayName = "Sit (Seiza)", Animation = "seiza", StopOnMovement = true,
            StopOnDamage = true
        },
        ["kneel"] = new CustomEmote
        {
            Code = "kneel", Name = "Kneel", DisplayName = "Kneel", Animation = "kneel", StopOnMovement = true,
            StopOnDamage = true
        },
        ["layingdown"] = new CustomEmote
        {
            Code = "layingdown", Name = "LayingDown", DisplayName = "Lay Down", Animation = "layingdown",
            StopOnMovement = true, StopOnDamage = true
        },
        ["surrender"] = new CustomEmote
        {
            Code = "surrender", Name = "Surrender", DisplayName = "Surrender", Animation = "surrender",
            StopOnMovement = true, StopOnDamage = true
        },
        ["atease"] = new CustomEmote
        {
            Code = "atease", Name = "AtEase", DisplayName = "At Ease", Animation = "atease", StopOnMovement = false,
            StopOnDamage = true
        },
        ["pointing"] = new CustomEmote
        {
            Code = "pointing", Name = "Pointing", DisplayName = "Point", Animation = "pointing", StopOnMovement = true,
            StopOnDamage = true
        },
        ["leaningcrossed"] = new CustomEmote
        {
            Code = "leaningcrossed", Name = "LeaningCrossed", DisplayName = "Lean (Arms Crossed)",
            Animation = "leaningcrossed", StopOnMovement = true, StopOnDamage = true
        },
        ["leaninghips"] = new CustomEmote
        {
            Code = "leaninghips", Name = "LeaningHips", DisplayName = "Lean (Hips)", Animation = "leaninghips",
            StopOnMovement = true, StopOnDamage = true
        },
        ["leaninghandshead"] = new CustomEmote
        {
            Code = "leaninghandshead", Name = "LeaningHandsHead", DisplayName = "Lean (Hands on Head)",
            Animation = "leaninghandshead", StopOnMovement = true, StopOnDamage = true
        },
        ["flippingoff"] = new CustomEmote
        {
            Code = "flippingoff", Name = "FlippingOff", DisplayName = "Flip Off", Animation = "flippingoff",
            StopOnMovement = true, StopOnDamage = true, StopAfterAnimation = true
        },
        ["crossedarmsthinking"] = new CustomEmote
        {
            Code = "crossedarmsthinking", Name = "CrossedArmsThinking", DisplayName = "Think",
            Animation = "crossedarmsthinking", StopOnMovement = true, StopOnDamage = true
        },
        ["sittingcool"] = new CustomEmote
        {
            Code = "sittingcool", Name = "SittingCool", DisplayName = "Sit (Cool)", Animation = "sittingcool",
            StopOnMovement = true, StopOnDamage = true
        },
        ["blowkiss"] = new CustomEmote
        {
            Code = "blowkiss", Name = "Blowkiss", DisplayName = "Blow Kiss", Animation = "blowkiss",
            StopOnMovement = true, StopOnDamage = true, StopAfterAnimation = true
        },
        ["chestthump"] = new CustomEmote
        {
            Code = "chestthump", Name = "ChestThump", DisplayName = "Chest Thump", Animation = "chestthump",
            StopOnMovement = true, StopOnDamage = true, StopAfterAnimation = true
        },
        ["clapping"] = new CustomEmote
        {
            Code = "clapping", Name = "Clapping", DisplayName = "Clap", Animation = "clapping", StopOnMovement = true,
            StopOnDamage = true, StopAfterAnimation = true
        },
        ["crossedarms"] = new CustomEmote
        {
            Code = "crossedarms", Name = "CrossedArms", DisplayName = "Cross Arms", Animation = "crossedarms",
            StopOnMovement = true, StopOnDamage = true
        },
        ["handshake"] = new CustomEmote
        {
            Code = "handshake", Name = "Handshake", DisplayName = "Handshake", Animation = "handshake",
            StopOnMovement = true, StopOnDamage = true, RequiresTarget = true,
            PairDistance = 0.75f
        },
        ["layingback"] = new CustomEmote
        {
            Code = "layingback", Name = "LayingBack", DisplayName = "Lay Back", Animation = "layingback",
            StopOnMovement = true, StopOnDamage = true
        },
        ["refinedsalute"] = new CustomEmote
        {
            Code = "refinedsalute", Name = "RefinedSalute", DisplayName = "Salute (Refined)",
            Animation = "refinedsalute",
            StopOnMovement = true, StopOnDamage = true
        },
        ["salute"] = new CustomEmote
        {
            Code = "salute", Name = "Salute", DisplayName = "Salute", Animation = "salute", StopOnMovement = true,
            StopOnDamage = true
        },
        ["scanning"] = new CustomEmote
        {
            Code = "scanning", Name = "Scanning", DisplayName = "Scan", Animation = "scanning", StopOnMovement = true,
            StopOnDamage = true
        },
        ["squatting"] = new CustomEmote
        {
            Code = "squatting", Name = "Squatting", DisplayName = "Squat", Animation = "squatting",
            StopOnMovement = true, StopOnDamage = true
        },
        ["thinkinghard"] = new CustomEmote
        {
            Code = "thinkinghard", Name = "ThinkingHard", DisplayName = "Think Hard", Animation = "thinkinghard",
            StopOnMovement = true, StopOnDamage = true
        },
        ["bringiton"] = new CustomEmote
        {
            Code = "bringiton", Name = "BringItOn", DisplayName = "Bring It On", Animation = "bringiton",
            StopOnMovement = true, StopOnDamage = true
        },
        ["slitthroat"] = new CustomEmote
        {
            Code = "slitthroat", Name = "SlitThroat", DisplayName = "Slit Throat", Animation = "slitthroat",
            StopOnMovement = true, StopOnDamage = true, StopAfterAnimation = true
        },
        ["prayer"] = new CustomEmote
        {
            Code = "prayer", Name = "Prayer", DisplayName = "Prayer", Animation = "prayer",
            StopOnMovement = true, StopOnDamage = true
        },
        ["handup"] = new CustomEmote
        {
            Code = "handup", Name = "HandUp", DisplayName = "Hand Up", Animation = "handup",
            StopOnMovement = true, StopOnDamage = true
        },
        ["victory"] = new CustomEmote
        {
            Code = "victory", Name = "Victory", DisplayName = "Victory", Animation = "victory",
            StopOnMovement = true, StopOnDamage = true
        },
        ["handrub"] = new CustomEmote
        {
            Code = "handrub", Name = "HandRub", DisplayName = "Rub Hands", Animation = "handrub",
            StopOnMovement = true, StopOnDamage = true, StopAfterAnimation = true
        },
        ["engarde"] = new CustomEmote
        {
            Code = "engarde", Name = "EnGarde", DisplayName = "En Garde", Animation = "engarde",
            StopOnMovement = true, StopOnDamage = true
        },
        ["dapup"] = new CustomEmote
        {
            Code = "dapup", Name = "DapUp", DisplayName = "Dap Up", Animation = "dapup",
            StopOnMovement = true, StopOnDamage = true, RequiresTarget = true, StopAfterAnimation = false,
            PairDistance = 0.75f
        },
        ["politebow"] = new CustomEmote
        {
            Code = "politebow", Name = "PoliteBow", DisplayName = "Polite Bow", Animation = "politebow",
            StopOnMovement = true, StopOnDamage = true
        },
        ["prayerstanding"] = new CustomEmote
        {
            Code = "prayerstanding", Name = "PrayerStanding", DisplayName = "Prayer (Standing)",
            Animation = "prayerstanding",
            StopOnMovement = true, StopOnDamage = true
        },
        ["kisshand"] = new CustomEmote
        {
            Code = "kisshand", Name = "KissHand", DisplayName = "Kiss Hand", Animation = "kisshand",
            StopOnMovement = true, StopOnDamage = true
        },
        ["knocking"] = new CustomEmote
        {
            Code = "knocking", Name = "Knocking", DisplayName = "Knock", Animation = "knocking",
            StopOnMovement = true, StopOnDamage = true, StopAfterAnimation = true
        },
        ["martialarts"] = new CustomEmote
        {
            Code = "martialarts", Name = "MartialArts", DisplayName = "Martial Arts", Animation = "martialarts",
            StopOnMovement = true, StopOnDamage = true
        },
        ["noblesalute"] = new CustomEmote
        {
            Code = "noblesalute", Name = "NobleSalute", DisplayName = "Salute (Noble)", Animation = "noblesalute",
            StopOnMovement = true, StopOnDamage = true
        },
        ["crackingknuckles"] = new CustomEmote
        {
            Code = "crackingknuckles", Name = "CrackingKnuckles", DisplayName = "Crack Knuckles",
            Animation = "crackingknuckles",
            StopOnMovement = true, StopOnDamage = true
        },
        ["sittingchill"] = new CustomEmote
        {
            Code = "sittingchill", Name = "SittingChill", DisplayName = "Sit (Chill)", Animation = "sittingchill",
            StopOnMovement = true, StopOnDamage = true
        },
        ["sittingrelaxed"] = new CustomEmote
        {
            Code = "sittingrelaxed", Name = "SittingRelaxed", DisplayName = "Sit (Relaxed)",
            Animation = "sittingrelaxed",
            StopOnMovement = true, StopOnDamage = true
        },
        ["sittingrefined"] = new CustomEmote
        {
            Code = "sittingrefined", Name = "SittingRefined", DisplayName = "Sit (Refined)",
            Animation = "sittingrefined",
            StopOnMovement = true, StopOnDamage = true
        },
        ["sittingcalm"] = new CustomEmote
        {
            Code = "sittingcalm", Name = "SittingCalm", DisplayName = "Sit (Calm)", Animation = "sittingcalm",
            StopOnMovement = true, StopOnDamage = true
        },
        ["sittinginnocent"] = new CustomEmote
        {
            Code = "sittinginnocent", Name = "SittingInnocent", DisplayName = "Sit (Innocent)",
            Animation = "sittinginnocent",
            StopOnMovement = true, StopOnDamage = true
        },
        ["laydownsensual"] = new CustomEmote
        {
            Code = "laydownsensual", Name = "LayDownSensual", DisplayName = "Lay Down (Sensual)",
            Animation = "laydownsensual",
            StopOnMovement = true, StopOnDamage = true
        },
        ["handships"] = new CustomEmote
        {
            Code = "handships", Name = "HandsHips", DisplayName = "Hands on Hips", Animation = "handships",
            StopOnMovement = true, StopOnDamage = true
        },
        ["hug"] = new CustomEmote
        {
            Code = "hug", Name = "Hug", DisplayName = "Hug (Intimate)", Animation = "hug",
            StopOnMovement = true, StopOnDamage = true, StopAfterAnimation = false, RequiresTarget = true,
            PairDistance = 0.1f
        },
        ["hugalone"] = new CustomEmote
        {
            Code = "hugalone", Name = "HugAlone", DisplayName = "Hug (Intimate)", Animation = "hug",
            StopOnMovement = true, StopOnDamage = true, StopAfterAnimation = false
        },
        ["kiss"] = new CustomEmote
        {
            Code = "kiss", Name = "Kiss", DisplayName = "Kiss (Intimate)", Animation = "kiss",
            StopOnMovement = true, StopOnDamage = true, StopAfterAnimation = true, RequiresTarget = true,
            PairDistance = 0.2f
        },
        ["hugfriendly"] = new CustomEmote
        {
            Code = "hugfriendly", Name = "HugFriendly", DisplayName = "Hug (Friendly)", Animation = "hugfriendly",
            StopOnMovement = true, StopOnDamage = true, StopAfterAnimation = false, RequiresTarget = true,
            PairDistance = 0.15f
        },
        ["hugfriendlyalone"] = new CustomEmote
        {
            Code = "hugfriendlyalone", Name = "HugFriendlyAlone", DisplayName = "Hug (Friendly)",
            Animation = "hugfriendly",
            StopOnMovement = true, StopOnDamage = true, StopAfterAnimation = false
        },
        ["smooch"] = new CustomEmote
        {
            Code = "smooch", Name = "Smooch", DisplayName = "Smooch", Animation = "smooch",
            StopOnMovement = true, StopOnDamage = true, StopAfterAnimation = true, RequiresTarget = true,
            PairDistance = 0.35f
        },
        ["handshakedouble"] = new CustomEmote
        {
            Code = "handshakedouble", Name = "HandshakeDouble", DisplayName = "Handshake (Double)",
            Animation = "handshakedouble",
            StopOnMovement = true, StopOnDamage = true, StopAfterAnimation = true, RequiresTarget = true,
            PairDistance = 0.75f
        },
        ["handholding"] = new CustomEmote
        {
            Code = "handholding", Name = "HandHolding", DisplayName = "Hold Hands", Animation = "handholding",
            StopOnMovement = true, StopOnDamage = true, RequiresTarget = true,
            PairDistance = 0.65f
        },
        ["handholdingintimate"] = new CustomEmote
        {
            Code = "handholdingintimate", Name = "HandHoldingIntimate", DisplayName = "Hold Hands (Intimate)",
            Animation = "handholdingintimate",
            StopOnMovement = true, StopOnDamage = true, RequiresTarget = true,
            PairDistance = 0.35f
        },
        ["sittingrested"] = new CustomEmote
        {
            Code = "sittingrested", Name = "SittingRested", DisplayName = "Sit (Rested)",
            Animation = "sittingrested",
            StopOnMovement = true, StopOnDamage = true
        },
        ["sittingintrovert"] = new CustomEmote
        {
            Code = "sittingintrovert", Name = "SittingIntrovert", DisplayName = "Sit (Introvert)",
            Animation = "sittingintrovert",
            StopOnMovement = true, StopOnDamage = true
        },
        ["leaningsimple"] = new CustomEmote
        {
            Code = "leaningsimple", Name = "LeaningSimple", DisplayName = "Lean (Simple)",
            Animation = "leaningsimple",
            StopOnMovement = true, StopOnDamage = true
        }
    };

    private static readonly HashSet<string> LeanEmotes = new()
        { "leaningcrossed", "leaninghips", "leaninghandshead", "leaningsimple" };

    private static readonly (BlockFacing facing, float yaw)[] HorizontalFacings =
    {
        (BlockFacing.NORTH, 0f),
        (BlockFacing.SOUTH, (float)Math.PI),
        (BlockFacing.EAST, -(float)(Math.PI / 2)),
        (BlockFacing.WEST, (float)(Math.PI / 2))
    };

    private readonly Dictionary<string, PairRequest> pairRequests = new();
    private ICoreClientAPI clientApi;

    private IClientNetworkChannel clientChannel;

    private EmotesConfig config;
    private ICoreServerAPI serverApi;
    private IServerNetworkChannel serverChannel;

    public static bool IsEmotePlaying { get; set; }

    public static IReadOnlyDictionary<string, CustomEmote> GetEmotes()
    {
        return Emotes;
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
        foreach (var code in Emotes.Keys)
        {
            tree.SetBool(code, false);
        }

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
            .SetMessageHandler<LeanSnapPacket>(OnLeanSnap);

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

        serverChannel = api.Network.RegisterChannel(ChannelName)
            .RegisterMessageType<EmotePacket>()
            .RegisterMessageType<LeanSnapPacket>()
            .SetMessageHandler<EmotePacket>(OnEmotePacket);

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

        cmd.BeginSubCommand("accept")
            .RequiresPlayer()
            .HandleWith(OnPairAccepted)
            .EndSubCommand();

        cmd.BeginSubCommand("refuse")
            .RequiresPlayer()
            .HandleWith(OnPairRefused)
            .EndSubCommand();

        foreach (var (code, _) in Emotes.Where(kv => kv.Value.RequiresTarget))
        {
            var capturedCode = code;
            cmd.BeginSubCommand(code)
                .RequiresPlayer()
                .HandleWith(args => OnPairInitiate(capturedCode, args))
                .EndSubCommand();
        }
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

        if (!isActive && LeanEmotes.Contains(packet.Code))
        {
            if (!TrySnapToWall(fromPlayer))
                tree.RemoveAttribute("leanYaw");
        }
        else
        {
            tree.RemoveAttribute("leanYaw");
        }

        foreach (var code in Emotes.Keys)
        {
            tree.SetBool(code, false);
        }

        if (!isActive)
        {
            tree.SetBool(packet.Code, true);
        }

        player.WatchedAttributes.MarkPathDirty("emotes");
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

        if (!config.PairedEmotesRequireConsent)
        {
            return ExecutePair(emoteCode, emote, initiatorPlayer, initiator, targetPlayer, target);
        }

        pairRequests[initiatorPlayer.PlayerUID] = new PairRequest(initiatorPlayer.PlayerUID, targetPlayer.PlayerUID,
            emoteCode, DateTime.Now);

        var accept = "<a href=\"command:///emotes accept\">Accept</a>";
        var refuse = "<a href=\"command:///emotes refuse\">Refuse</a>";
        targetPlayer.SendMessage(GlobalConstants.CurrentChatGroup,
            Lang.Get("emotes:pair-request-received", initiator.GetName(), Lang.Get("emotes:emote-" + emoteCode), accept,
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
        if (!Emotes.TryGetValue(key, out var emote) || emote.RequiresTarget)
        {
            return TextCommandResult.Error(Lang.Get("emotes:cmd-not-found", key));
        }

        var tree = player.WatchedAttributes.GetOrAddTreeAttribute("emotes");
        var isActive = tree.GetBool(emote.Code);
        foreach (var code in Emotes.Keys)
        {
            tree.SetBool(code, false);
        }

        if (!isActive)
        {
            tree.SetBool(emote.Code, true);
        }

        player.WatchedAttributes.MarkPathDirty("emotes");

        var displayName = Lang.Get("emotes:emote-" + emote.Code);
        return TextCommandResult.Success(isActive
            ? Lang.Get("emotes:cmd-emote-stopped", displayName)
            : Lang.Get("emotes:cmd-emote-started", displayName));
    }

    private TextCommandResult HandleListCommand(TextCommandCallingArgs args)
    {
        return TextCommandResult.Success(Lang.Get("emotes:cmd-available-emotes",
            string.Join(", ",
                Emotes.Values.Where(e => !e.RequiresTarget)
                    .Select(e => $"{e.Code} ({Lang.Get("emotes:emote-" + e.Code)})"))));
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

        var codes = Emotes.Keys.ToList();
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
            foreach (var code in Emotes.Keys)
            {
                t.SetBool(code, false);
            }

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