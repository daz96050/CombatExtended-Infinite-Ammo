using HarmonyLib;
using CombatExtended;

namespace CombatExtendedInfiniteAmmo.Patches;

/// <summary>
/// Shows a full bar with "∞" on the ammo gizmo while the weapon cannot run out.
/// GizmoAmmoStatus is a Gizmo_Slider, so only its bar values are patched and CE keeps drawing it.
/// </summary>
public static class GizmoAmmoStatusHelper
{
    public static bool IsInfinite(GizmoAmmoStatus gizmo)
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        var comp = gizmo.compAmmo;
        if (settings == null || comp == null) return false;

        if (comp.turret != null)
        {
            return comp.CurMagCount > 0 &&
                   (AmmoInventoryHelper.IsInfiniteTurret(comp, settings) ||
                    AmmoInventoryHelper.IsInfiniteMortar(comp, settings));
        }

        // HasAmmo is already true when no ammo is required
        return settings.infiniteAmmo && AmmoInventoryHelper.IsInfinitePawn(comp, settings) && comp.HasAmmo;
    }
}

[HarmonyPatch(typeof(GizmoAmmoStatus), nameof(GizmoAmmoStatus.ValuePercent), MethodType.Getter)]
public static class Patch_GizmoAmmoStatus_ValuePercent
{
    public static void Postfix(GizmoAmmoStatus __instance, ref float __result)
    {
        if (GizmoAmmoStatusHelper.IsInfinite(__instance)) __result = 1f;
    }
}

[HarmonyPatch(typeof(GizmoAmmoStatus), nameof(GizmoAmmoStatus.BarLabel), MethodType.Getter)]
public static class Patch_GizmoAmmoStatus_BarLabel
{
    public static void Postfix(GizmoAmmoStatus __instance, ref string __result)
    {
        if (GizmoAmmoStatusHelper.IsInfinite(__instance)) __result = "∞";
    }
}

// The opportunistic-reload slider means nothing for a magazine that never empties
[HarmonyPatch(typeof(GizmoAmmoStatus), nameof(GizmoAmmoStatus.IsDraggable), MethodType.Getter)]
public static class Patch_GizmoAmmoStatus_IsDraggable
{
    public static void Postfix(GizmoAmmoStatus __instance, ref bool __result)
    {
        if (__result && GizmoAmmoStatusHelper.IsInfinite(__instance)) __result = false;
    }
}
