using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Emotes;

public class EmotesModSystem : ModSystem
{
    public override void StartServerSide(ICoreServerAPI api)
    {
        var chatCommands = api.ChatCommands;
        var parsers = api.ChatCommands.Parsers;
        chatCommands.GetOrCreate("emotes").RequiresPrivilege(Privilege.chat)
            .WithDescription("Execute an emote on your player")
            .WithArgs(parsers.OptionalWord("emote"))
            .HandleWith(delegate(TextCommandCallingArgs args)
            {
                var entity = args.Caller.Entity;
                if (entity is not EntityPlayer player)
                {
                    return TextCommandResult.Error("Only players can choose emotes");
                }

                if (player.AnimManager.Animator is not ServerAnimator serverAnimator)
                {
                    return TextCommandResult.Error("Command not executing on server side");
                }

                var input = (string)args[0];

                // TODO: optimize and cache beforehand
                var runningAnimation =
                    serverAnimator.anims.FirstOrDefault(anim =>
                        string.Equals(anim.Animation.Name, input, StringComparison.CurrentCultureIgnoreCase));

                if (input == null || runningAnimation == null)
                {
                    return TextCommandResult.Error(Lang.Get("Choose emote: {0}",
                        string.Join(", ",
                            serverAnimator.anims.Select(anim => anim.Animation.Name).Order().ToList())));
                }

                if (input != "shakehead" && !player.RightHandItemSlot.Empty &&
                    player.RightHandItemSlot.Itemstack.Collectible.GetHeldTpIdleAnimation(player.RightHandItemSlot,
                        player,
                        (EnumHand)1) != null)
                {
                    return TextCommandResult.Error("Only with free hands");
                }

                api.Network.BroadcastEntityPacket(player.EntityId, 197,
                    SerializerUtil.Serialize(runningAnimation.Animation.Name));
                return TextCommandResult.Success();
            });
    }
}