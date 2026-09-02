using System;
using System.Linq;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace Emotes;

public static class CombatOverhaulPatch
{
    private const string HarmonyId = "emotes.overhaullib";
    private static Harmony _harmony;

    public static void Apply(ICoreClientAPI api)
    {
        var overhaulAsm = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name.Equals("overhaullib", StringComparison.OrdinalIgnoreCase));
        if (overhaulAsm == null)
        {
            return;
        }

        var fpType = overhaulAsm.GetType("CombatOverhaul.Animations.FirstPersonAnimationsBehavior");
        var tpType = overhaulAsm.GetType("CombatOverhaul.Animations.ThirdPersonAnimationsBehavior");
        if (fpType == null && tpType == null)
        {
            return;
        }

        _harmony = new Harmony(HarmonyId);
        var prefix = new HarmonyMethod(typeof(CombatOverhaulPatch), nameof(SkipIfEmotePlaying));
        var paramTypes = new[] { typeof(Entity), typeof(ElementPose), typeof(AnimatorBase) };

        var fpOnFrame = fpType?.GetMethod("OnFrame", paramTypes);
        var tpOnFrame = tpType?.GetMethod("OnFrame", paramTypes);

        if (fpOnFrame != null)
        {
            _harmony.Patch(fpOnFrame, prefix);
        }

        if (tpOnFrame != null)
        {
            _harmony.Patch(tpOnFrame, prefix);
        }
    }

    public static void Remove()
    {
        _harmony?.UnpatchAll(HarmonyId);
    }

    private static bool SkipIfEmotePlaying(Entity __0)
    {
        return !EmoteState.IsEmoting(__0);
    }
}