using HarmonyLib;
using Verse;
using CombatExtended;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace CombatExtendedInfiniteAmmo.Patches;

/// <summary>
/// CE's "reuse neolithic projectiles" setting drops a recoverable arrow (or similar) where it lands.
/// When the shot came from a weapon with infinite ammo, nothing was used up, so the dropped arrow
/// would be free ammo. The setting's check in ProjectileCE.Impact is rewritten to also ask whether
/// the firing weapon is infinite.
/// </summary>
[HarmonyPatch(typeof(ProjectileCE), nameof(ProjectileCE.Impact))]
public static class Patch_ProjectileCE_Impact
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo reuseGetter = AccessTools.PropertyGetter(typeof(Settings), nameof(Settings.ReuseNeolithicProjectiles));
        MethodInfo filter = AccessTools.Method(typeof(Patch_ProjectileCE_Impact), nameof(ShouldDropProjectile));
        bool replaced = false;

        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.Calls(reuseGetter))
            {
                replaced = true;
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Call, filter);
            }
        }

        if (!replaced)
            Log.Warning("[CE Infinite Ammo] Could not find CE's projectile reuse check; arrows from infinite weapons may still drop.");
    }

    public static bool ShouldDropProjectile(bool reuseEnabled, ProjectileCE projectile)
    {
        if (!reuseEnabled) return false;

        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        var comp = (projectile.equipment as ThingWithComps)?.TryGetComp<CompAmmoUser>();
        if (settings == null || comp == null) return true;

        bool infinite = comp.turret != null
            ? AmmoInventoryHelper.IsInfiniteTurret(comp, settings) || AmmoInventoryHelper.IsInfiniteMortar(comp, settings)
            : AmmoInventoryHelper.IsInfinitePawn(comp, settings);
        return !infinite;
    }
}
