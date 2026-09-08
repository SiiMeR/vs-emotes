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
    public const string PairPartnerEntityKey = "pairPartnerEntity";

    public static ITreeAttribute Tree(Entity entity)
    {
        return entity.WatchedAttributes.GetOrAddTreeAttribute(TreeKey);
    }

    public static bool InCarry(Entity entity)
    {
        var attributes = entity?.WatchedAttributes;
        if (attributes == null) return false;

        return attributes.GetBool("carrying") || attributes.GetBool("carried");
    }

    public static void MarkDirty(Entity entity)
    {
        if (entity == null) return;

        entity.WatchedAttributes.MarkPathDirty(TreeKey);
        entity.WatchedAttributes.SetBool(EmotingKey, IsEmoting(entity));
        entity.WatchedAttributes.MarkPathDirty(EmotingKey);
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

    public static bool HasPartner(ITreeAttribute tree)
    {
        if (tree == null) return false;

        return !string.IsNullOrEmpty(tree.GetString(PairPartnerKey)) || tree.GetLong(PairPartnerEntityKey) != 0;
    }

    public static bool HasPartner(Entity entity)
    {
        return HasPartner(entity?.WatchedAttributes?.GetTreeAttribute(TreeKey));
    }

    public static void ClearPartner(Entity entity)
    {
        if (entity == null) return;

        var tree = Tree(entity);
        tree.SetString(PairPartnerKey, "");
        tree.SetLong(PairPartnerEntityKey, 0);
        entity.WatchedAttributes.MarkPathDirty(TreeKey);
    }

    public static void ClearBools(ITreeAttribute tree)
    {
        if (tree == null) return;

        foreach (var attribute in tree)
            if (attribute.Value is BoolAttribute boolAttribute)
                boolAttribute.value = false;
    }

    public static void Set(Entity entity, string code, bool active)
    {
        if (entity == null) return;

        Tree(entity).SetBool(code, active);
        MarkDirty(entity);
    }

    public static void Play(Entity entity, string code)
    {
        if (entity == null) return;

        var tree = Tree(entity);
        ClearBools(tree);
        tree.SetBool(code, true);
        MarkDirty(entity);
    }

    public static bool Toggle(Entity entity, string code)
    {
        if (entity == null) return false;

        var tree = Tree(entity);
        var isActive = tree.GetBool(code);
        ClearBools(tree);
        if (!isActive) tree.SetBool(code, true);
        MarkDirty(entity);
        return !isActive;
    }

    public static void StopAll(Entity entity)
    {
        if (entity == null) return;

        var tree = Tree(entity);
        ClearBools(tree);
        tree.RemoveAttribute(LeanYawKey);
        MarkDirty(entity);
    }
}
