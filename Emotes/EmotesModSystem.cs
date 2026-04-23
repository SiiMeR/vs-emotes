using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

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
            StopOnMovement = true, StopOnDamage = true
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
            StopOnMovement = true, StopOnDamage = true
        },
        ["layingback"] = new CustomEmote
        {
            Code = "layingback", Name = "LayingBack", DisplayName = "Lay Back", Animation = "layingback",
            StopOnMovement = true, StopOnDamage = true
        },
        ["refinedsalute"] = new CustomEmote
        {
            Code = "refinedsalute", Name = "RefinedSalute", DisplayName = "Salute (Refined)", Animation = "refinedsalute",
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
            StopOnMovement = true, StopOnDamage = true
        },
        ["politebow"] = new CustomEmote
        {
            Code = "politebow", Name = "PoliteBow", DisplayName = "Polite Bow", Animation = "politebow",
            StopOnMovement = true, StopOnDamage = true
        },
        ["prayerstanding"] = new CustomEmote
        {
            Code = "prayerstanding", Name = "PrayerStanding", DisplayName = "Prayer (Standing)", Animation = "prayerstanding",
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
            Code = "crackingknuckles", Name = "CrackingKnuckles", DisplayName = "Crack Knuckles", Animation = "crackingknuckles",
            StopOnMovement = true, StopOnDamage = true
        },
    };

    private static readonly HashSet<string> LeanEmotes = new() { "leaningcrossed", "leaninghips", "leaninghandshead" };

    private static readonly (BlockFacing facing, float yaw)[] HorizontalFacings =
    {
        (BlockFacing.NORTH, 0f),
        (BlockFacing.SOUTH, (float)Math.PI),
        (BlockFacing.EAST, -(float)(Math.PI / 2)),
        (BlockFacing.WEST, (float)(Math.PI / 2))
    };

    private ICoreClientAPI clientApi;

    private IClientNetworkChannel clientChannel;
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

        player.WatchedAttributes.MarkPathDirty("emotes");
    }

    public void SendToggleEmote(string code)
    {
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
        if (clientApi?.ModLoader.IsModEnabled("overhaullib") == true) CombatOverhaulPatch.Remove();
        base.Dispose();
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);
        clientApi = api;
        if (api.ModLoader.IsModEnabled("overhaullib")) CombatOverhaulPatch.Apply(api);

        clientChannel = api.Network.RegisterChannel(ChannelName)
            .RegisterMessageType<EmotePacket>()
            .RegisterMessageType<LeanSnapPacket>()
            .SetMessageHandler<LeanSnapPacket>(OnLeanSnap);

        api.Input.RegisterHotKey("emotepicker", "Open Emote Picker", GlKeys.J, shiftPressed: true);
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

        serverChannel = api.Network.RegisterChannel(ChannelName)
            .RegisterMessageType<EmotePacket>()
            .RegisterMessageType<LeanSnapPacket>()
            .SetMessageHandler<EmotePacket>(OnEmotePacket);

        api.ChatCommands
            .GetOrCreate("emotes")
            .RequiresPrivilege(Privilege.chat)
            .WithDescription("Play an emote. Usage: /emotes <name|list|stop>")
            .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"))
            .HandleWith(HandleEmoteCommand);
    }

    private void OnEmotePacket(IServerPlayer fromPlayer, EmotePacket packet)
    {
        if (fromPlayer.Entity is not EntityPlayer player) return;

        if (packet.ForceStop)
        {
            StopAllEmotes(player);
            return;
        }

        if (!Emotes.ContainsKey(packet.Code)) return;

        var tree = player.WatchedAttributes.GetOrAddTreeAttribute("emotes");
        var isActive = tree.GetBool(packet.Code);

        if (!isActive && LeanEmotes.Contains(packet.Code))
            TrySnapToWall(fromPlayer);

        foreach (var code in Emotes.Keys)
            tree.SetBool(code, false);

        if (!isActive)
            tree.SetBool(packet.Code, true);

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
        var pos = entity.ServerPos;
        var blockPos = pos.AsBlockPos;

        foreach (var (facing, yaw) in HorizontalFacings)
        {
            var norm = facing.Normali;
            BlockPos wallPos = null;
            for (int dist = 1; dist <= 2; dist++)
            {
                var candidate = blockPos.AddCopy(norm.X * dist, 0, norm.Z * dist);
                if (IsSolidWall(world, candidate, facing))
                {
                    wallPos = candidate;
                    break;
                }
            }

            if (wallPos == null) continue;

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
            entity.ServerPos.Yaw = yaw;
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

    private TextCommandResult HandleEmoteCommand(TextCommandCallingArgs args)
    {
        if (args.Caller.Entity is not EntityPlayer player)
        {
            return TextCommandResult.Error("Only players can use emotes");
        }

        var input = (string)args[0];

        if (string.IsNullOrEmpty(input) || input.Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            return TextCommandResult.Success("Available emotes: " + string.Join(", ", Emotes.Values.Select(e => $"{e.Code} ({e.DisplayName})")));
        }

        if (input.Equals("stop", StringComparison.OrdinalIgnoreCase) ||
            input.Equals("stopall", StringComparison.OrdinalIgnoreCase))
        {
            StopAllEmotes(player);
            return TextCommandResult.Success("All emotes stopped");
        }

        if (input.Equals("showcase", StringComparison.OrdinalIgnoreCase))
        {
            var codes = Emotes.Keys.ToList();
            var showcaseApi = player.Api;

            void RunNext(int index)
            {
                if (!player.Alive) return;
                if (index >= codes.Count)
                {
                    StopAllEmotes(player);
                    return;
                }
                var t = player.WatchedAttributes.GetOrAddTreeAttribute("emotes");
                foreach (var code in Emotes.Keys)
                    t.SetBool(code, false);
                t.SetBool(codes[index], true);
                player.WatchedAttributes.MarkPathDirty("emotes");
                showcaseApi.Event.RegisterCallback(_ =>
                {
                    if (!player.Alive) return;
                    StopAllEmotes(player);
                    showcaseApi.Event.RegisterCallback(_ => RunNext(index + 1), 1000);
                }, 3000);
            }

            RunNext(0);
            return TextCommandResult.Success("Starting emote showcase...");
        }

        var key = input.ToLowerInvariant();
        if (!Emotes.TryGetValue(key, out var emote))
        {
            return TextCommandResult.Error($"Emote '{input}' not found. Use '/emotes list' to see available emotes.");
        }

        var tree = player.WatchedAttributes.GetOrAddTreeAttribute("emotes");
        var isActive = tree.GetBool(emote.Code);

        foreach (var code in Emotes.Keys)
            tree.SetBool(code, false);

        if (!isActive)
            tree.SetBool(emote.Code, true);

        player.WatchedAttributes.MarkPathDirty("emotes");
        return TextCommandResult.Success(isActive ? $"Stopped emote: {emote.Name}" : $"Started emote: {emote.Name}");
    }
}