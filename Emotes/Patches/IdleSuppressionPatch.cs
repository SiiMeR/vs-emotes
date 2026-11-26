using System.Linq;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace Emotes;

public static class IdleSuppressionPatch
{
    private const string HarmonyId = "emotes.idlesuppression";
    private const string ShiverPrefix = "coldidle";
    private const string ProtectEyes = "protecteyes";
    private const string FpEnding = "-fp";
    private static Harmony harmony;

    public static void Apply()
    {
        if (harmony != null) return;

        var target = AccessTools.Method(typeof(AnimationManager), nameof(AnimationManager.StartAnimation), new[] { typeof(AnimationMetaData) });
        if (target == null) return;

        harmony = new Harmony(HarmonyId);
        harmony.Patch(target, new HarmonyMethod(typeof(IdleSuppressionPatch), nameof(SkipWhileEmoting)));
    }

    public static void Remove()
    {
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
    }

    public static void StopSuppressed(Entity entity, IAnimationManager manager)
    {
        var active = manager?.ActiveAnimationsByAnimCode;
        if (entity == null || active == null) return;
        if (entity is EntityPlayer player && player.OtherAnimManager == null) return;

        var codes = active.Values
            .Where(anim => IsSuppressed(entity, anim))
            .Select(anim => anim.Code)
            .ToArray();
        foreach (var code in codes) manager.StopAnimation(code);
    }

    private static bool SkipWhileEmoting(AnimationManager __instance, Entity ___entity, AnimationMetaData animdata, ref bool __result)
    {
        if (___entity is not EntityPlayer) return true;
        if (animdata?.Animation == null || __instance.ActiveAnimationsByAnimCode.ContainsKey(animdata.Animation)) return true;
        if (!EmoteState.IsEmoting(___entity)) return true;
        if (!IsSuppressed(___entity, animdata)) return true;

        __result = false;
        return false;
    }

    private static bool IsSuppressed(Entity entity, AnimationMetaData anim)
    {
        var code = anim?.Code;
        if (code == null) return false;
        if (code.EndsWith(FpEnding)) code = code[..^FpEnding.Length];
        if (code.StartsWith(ShiverPrefix) || code == ProtectEyes) return true;

        var randomIdle = entity.Properties?.Attributes?["randomIdleAnimations"];
        if (randomIdle?.Exists != true) return false;

        return randomIdle.AsArray<string>()?.Contains(code) == true;
    }
}
