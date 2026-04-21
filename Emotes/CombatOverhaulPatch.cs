using CombatOverhaul.Animations;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace Emotes;

public static class CombatOverhaulPatch
{
    private const string HarmonyId = "emotes.combatoverhaul";
    private static Harmony _harmony;

    public static void Apply(ICoreClientAPI api)
    {
        _harmony = new Harmony(HarmonyId);
        var prefix = new HarmonyMethod(typeof(CombatOverhaulPatch), nameof(SkipIfEmotePlaying));

        var fpOnFrame = typeof(FirstPersonAnimationsBehavior).GetMethod(
            "OnFrame", [typeof(Entity), typeof(ElementPose), typeof(AnimatorBase)]);
        var tpOnFrame = typeof(ThirdPersonAnimationsBehavior).GetMethod(
            "OnFrame", [typeof(Entity), typeof(ElementPose), typeof(AnimatorBase)]);

        if (fpOnFrame != null) _harmony.Patch(fpOnFrame, prefix: prefix);
        if (tpOnFrame != null) _harmony.Patch(tpOnFrame, prefix: prefix);
    }

    public static void Remove()
    {
        _harmony?.UnpatchAll(HarmonyId);
    }

    private static bool SkipIfEmotePlaying() => !EmotesModSystem.IsEmotePlaying;
}
