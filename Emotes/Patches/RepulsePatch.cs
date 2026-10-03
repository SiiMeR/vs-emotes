using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace Emotes;

public static class RepulsePatch
{
    private const string HarmonyId = "emotes.repulse";
    private static Harmony harmony;

    public static void Apply()
    {
        if (harmony != null) return;

        var target = AccessTools.Method(typeof(EntityBehaviorRepulseAgents), nameof(EntityBehaviorRepulseAgents.OnGameTick));
        if (target == null) return;

        harmony = new Harmony(HarmonyId);
        harmony.Patch(target, new HarmonyMethod(typeof(RepulsePatch), nameof(SkipWhilePaired)));
    }

    public static void Remove()
    {
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
    }

    private static bool SkipWhilePaired(EntityBehavior __instance)
    {
        var attributes = __instance?.entity?.WatchedAttributes;
        if (attributes == null || !attributes.GetBool(EmoteState.EmotingKey)) return true;

        return !EmoteState.HasPartner(__instance.entity);
    }
}
