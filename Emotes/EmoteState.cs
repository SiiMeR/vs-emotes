using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace Emotes;

public static class EmoteState
{
    public const string TreeKey = "emotes";
    public const string EmotingKey = "emoting";
    public const string LeanYawKey = "leanYaw";
    public const string PairYawKey = "pairYaw";
    public const string PairPartnerKey = "pairPartner";

    public static ITreeAttribute Tree(EntityPlayer player)
    {
        return player.WatchedAttributes.GetOrAddTreeAttribute(TreeKey);
    }

    public static bool InCarry(Entity entity)
    {
        var attributes = entity?.WatchedAttributes;
        if (attributes == null) return false;

        return attributes.GetBool("carrying") || attributes.GetBool("carried");
    }

    public static void MarkDirty(EntityPlayer player)
    {
        if (player == null) return;

        player.WatchedAttributes.MarkPathDirty(TreeKey);
        player.WatchedAttributes.SetBool(EmotingKey, IsEmoting(player));
        player.WatchedAttributes.MarkPathDirty(EmotingKey);
    }

    public static bool IsEmoting(Entity entity)
    {
        var tree = entity?.WatchedAttributes?.GetTreeAttribute(TreeKey);
        if (tree == null) return false;

        foreach (var attribute in tree)
            if (attribute.Value is BoolAttribute { value: true })
                return true;

        return false;
    }

    public static void ClearBools(ITreeAttribute tree)
    {
        if (tree == null) return;

        foreach (var attribute in tree)
            if (attribute.Value is BoolAttribute boolAttribute)
                boolAttribute.value = false;
    }

    public static void Set(EntityPlayer player, string code, bool active)
    {
        if (player == null) return;

        Tree(player).SetBool(code, active);
        MarkDirty(player);
    }

    public static void Play(EntityPlayer player, string code)
    {
        if (player == null) return;

        var tree = Tree(player);
        ClearBools(tree);
        tree.SetBool(code, true);
        MarkDirty(player);
    }

    public static bool Toggle(EntityPlayer player, string code)
    {
        if (player == null) return false;

        var tree = Tree(player);
        var isActive = tree.GetBool(code);
        ClearBools(tree);
        if (!isActive) tree.SetBool(code, true);
        MarkDirty(player);
        return !isActive;
    }

    public static void StopAll(EntityPlayer player)
    {
        if (player == null) return;

        var tree = Tree(player);
        ClearBools(tree);
        tree.RemoveAttribute(LeanYawKey);
        MarkDirty(player);
    }
}
