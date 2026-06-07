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
/// Without this patch, the loadout system counts ammo in the weapon's magazine as part of the pawn's
/// "inventory" for loadout purposes. This means if a pawn has rounds loaded in their weapon but none
/// in their actual inventory, the loadout thinks they're fully stocked and won't trigger re-arm.
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
        if (settings == null) return;
        
        // Only apply when infinite ammo is active (infinite reserve keeps real rounds in inventory)
        if (!settings.infiniteAmmo) return;
        
        // Only apply for player faction if that setting is enabled
        if (settings.playerFactionOnly && pawn?.Faction != Faction.OfPlayer) return;
        
        // Get the primary weapon's CompAmmoUser
        Pawn_EquipmentTracker equipment = pawn?.equipment;
        if (equipment?.Primary == null) return;
        
        CompAmmoUser compAmmoUser = equipment.Primary.TryGetComp<CompAmmoUser>();
        if (compAmmoUser == null || !compAmmoUser.UseAmmo || compAmmoUser.CurrentAmmo == null) return;
        if (!compAmmoUser.HasMagazine) return;
        
        ThingDef ammoKey = compAmmoUser.CurrentAmmo;
        int magCount = compAmmoUser.CurMagCount;
        
        if (magCount <= 0) return;
        if (!__result.ContainsKey(ammoKey)) return;
        
        // Subtract the magazine ammo from the loadout count so it doesn't satisfy the loadout requirement
        __result[ammoKey].value -= magCount;
        
        // If the count dropped to zero or below, remove the entry entirely
        if (__result[ammoKey].value <= 0)
        {
            __result.Remove(ammoKey);
        }
    }
}


