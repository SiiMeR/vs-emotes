using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.Client.NoObf;

namespace Emotes;

public static class WheelMouseGrabPatch
{
    private const string HarmonyId = "emotes.wheel";
    private static Harmony harmony;

    public static void Apply()
    {
        if (harmony != null) return;

        var target = AccessTools.Method(typeof(ClientMain), nameof(ClientMain.UpdateFreeMouse));
        var wheelTarget = AccessTools.Method(typeof(ClientMain), nameof(ClientMain.OnMouseWheel));
        if (target == null || wheelTarget == null) return;

        harmony = new Harmony(HarmonyId);
        harmony.Patch(target, new HarmonyMethod(typeof(WheelMouseGrabPatch), nameof(FreeMouseWhileOpen)));
        harmony.Patch(wheelTarget, new HarmonyMethod(typeof(WheelMouseGrabPatch), nameof(ScrollWheelWhileOpen)));
    }

    public static void Remove()
    {
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
    }

    private static bool FreeMouseWhileOpen(ClientMain __instance)
    {
        if (__instance?.api?.ModLoader?.GetModSystem<EmotesModSystem>()?.WheelOpen != true) return true;

        if (__instance.MouseGrabbed) __instance.MouseGrabbed = false;
        __instance.mouseWorldInteractAnyway = false;
        return false;
    }

    private static bool ScrollWheelWhileOpen(ClientMain __instance, MouseWheelEventArgs args)
    {
        var system = __instance?.api?.ModLoader?.GetModSystem<EmotesModSystem>();
        if (system?.WheelOpen != true) return true;

        system.OnWheelScroll(args);
        return !args.IsHandled;
    }
}
