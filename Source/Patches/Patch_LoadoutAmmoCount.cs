using HarmonyLib;
using Verse;
using RimWorld;
using CombatExtended;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CombatExtendedInfiniteAmmo.Patches;

/// <summary>
/// Patches Utility_HoldTracker.GetStorageByThingDef to exclude magazine ammo from loadout counting
/// when infinite ammo or infinite reserve is active.
///
/// CE counts rounds loaded in a weapon as part of the pawn's ammo for loadout purposes, assuming
/// they came out of the inventory. In both infinite modes loading is free, so:
/// - Infinite Ammo: a loaded weapon would satisfy the loadout and the pawn never re-arms.
/// - Infinite Reserve: after every reload, inventory + magazine exceeds the loadout and the pawn
///   drops the "excess" from its inventory, until it has nothing left to reload with.
/// </summary>
[HarmonyPatch]
public static class Patch_GetStorageByThingDef
{
    public static MethodBase TargetMethod()
    {
        // Utility_HoldTracker is internal, so we need to use reflection
        Type holdTrackerType = AccessTools.TypeByName("CombatExtended.Utility_HoldTracker");
        return AccessTools.Method(holdTrackerType, "GetStorageByThingDef");
    }

    public static void Postfix(Pawn pawn, Dictionary<ThingDef, Integer> __result)
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings == null || settings.ammoMode == InfiniteAmmoMode.Off || pawn == null) return;

        // Only apply for player faction if that setting is enabled
        if (settings.playerFactionOnly && pawn.Faction != Faction.OfPlayer) return;

        SubtractMagazine(pawn.equipment?.Primary, __result);

        var inventory = pawn.inventory?.innerContainer;
        if (inventory == null) return;
        for (int i = 0; i < inventory.Count; i++)
        {
            SubtractMagazine(inventory[i].GetInnerIfMinified(), __result);
        }
    }

    private static void SubtractMagazine(Thing weapon, Dictionary<ThingDef, Integer> storage)
    {
        if (weapon is not ThingWithComps) return;

        CompAmmoUser comp = weapon.TryGetComp<CompAmmoUser>();
        if (comp == null || !comp.UseAmmo || comp.CurrentAmmo == null) return;

        int magCount = comp.CurMagCount;
        if (magCount <= 0) return;

        ThingDef ammoKey = comp.CurrentAmmo;
        if (!storage.TryGetValue(ammoKey, out Integer count)) return;

        // Subtract the magazine ammo from the loadout count so it doesn't satisfy the loadout requirement
        count.value -= magCount;

        // If the count dropped to zero or below, remove the entry entirely
        if (count.value <= 0)
        {
            storage.Remove(ammoKey);
        }
    }
}
