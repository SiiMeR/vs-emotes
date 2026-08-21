using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace Emotes;

public static class CombatOverhaulPatch
{
    private const string HarmonyId = "emotes.overhaullib";
    private static Harmony _harmony;

    public static void Apply()
    {
        var overhaulAsm = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name.Equals("overhaullib", StringComparison.OrdinalIgnoreCase));
        if (overhaulAsm == null)
        {
            return;
        }

        var typeNames = new[]
        {
            "CombatOverhaul.Animations.FirstPersonAnimationsBehavior",
            "CombatOverhaul.Animations.ThirdPersonAnimationsBehavior"
        };

        _harmony = new Harmony(HarmonyId);
        var prefix = new HarmonyMethod(typeof(CombatOverhaulPatch), nameof(SkipIfEmotePlaying));

        foreach (var typeName in typeNames)
        {
            var type = overhaulAsm.GetType(typeName);
            if (type == null)
            {
                continue;
            }

            foreach (var target in type.GetMethods(AccessTools.all).Where(IsEntityOnFrame))
            {
                _harmony.Patch(target, prefix);
            }
        }
    }

    private static bool IsEntityOnFrame(MethodInfo method)
    {
        if (method.Name != "OnFrame" || method.IsAbstract)
        {
            return false;
        }

        var parameters = method.GetParameters();
        return parameters.Length > 0 && typeof(Entity).IsAssignableFrom(parameters[0].ParameterType);
    }

    public static void Remove()
    {
        _harmony?.UnpatchAll(HarmonyId);
    }

    private static bool SkipIfEmotePlaying(Entity __0)
    {
        return !EmotesModSystem.IsEntityEmoting(__0);
    }
}