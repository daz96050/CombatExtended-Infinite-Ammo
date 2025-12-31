using HarmonyLib;
using Verse;
using RimWorld;
using CombatExtended;
using CombatExtended.Compatibility;
using System.Linq;

namespace CombatExtendedInfiniteAmmo.Patches;

public static class AmmoInventoryHelper
{
    // Check if pawn has ANY compatible ammo in inventory
    public static bool HasAnyAmmoInInventory(CompAmmoUser comp)
    {
        if (comp == null) return false;
        return comp.TryFindAmmoInInventory(out _);
    }
    
    // Check if pawn has SPECIFIC ammo type in inventory
    public static bool HasSpecificAmmoInInventory(CompAmmoUser comp, AmmoDef ammoDef)
    {
        if (comp == null || ammoDef == null) return false;
        
        Pawn wielder = comp.Wielder;
        if (wielder == null) return false;
        
        var inventory = wielder.inventory?.innerContainer;
        if (inventory == null) return false;
        
        return inventory.Any(t => t.def == ammoDef);
    }
    
    // Find specific ammo type in inventory, return the Thing
    public static Thing FindSpecificAmmoInInventory(CompAmmoUser comp, AmmoDef ammoDef)
    {
        if (comp == null || ammoDef == null) return null;
        
        Pawn wielder = comp.Wielder;
        if (wielder == null) return null;
        
        var inventory = wielder.inventory?.innerContainer;
        if (inventory == null) return null;
        
        return inventory.FirstOrDefault(t => t.def == ammoDef);
    }
}

[HarmonyPatch(typeof(CompAmmoUser), nameof(CompAmmoUser.Notify_ShotFired))]
public static class Patch_CompAmmoUser_Notify_ShotFired
{
    public static bool Prefix(CompAmmoUser __instance)
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings == null) return true;
        
        bool isTurret = __instance.turret != null;
        
        if (settings.playerFactionOnly)
        {
            Faction playerFaction = Faction.OfPlayer;
            if (isTurret && __instance.turret.Faction != playerFaction) return true;
            if (!isTurret && __instance.Wielder?.Faction != playerFaction) return true;
        }
        
        if (isTurret && settings.infiniteTurretAmmo && __instance.CurMagCount > 0)
        {
            return false;
        }
        
        if (!isTurret && settings.infiniteAmmo)
        {
            AmmoDef currentAmmo = __instance.CurrentAmmo;
            if (currentAmmo != null && AmmoInventoryHelper.HasSpecificAmmoInInventory(__instance, currentAmmo))
            {
                return false;
            }
        }
        
        return true;
    }
}

[HarmonyPatch(typeof(CompAmmoUser), nameof(CompAmmoUser.TryStartReload))]
public static class Patch_CompAmmoUser_TryStartReload
{
    public static bool Prefix(CompAmmoUser __instance)
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings == null) return true;
        
        bool isTurret = __instance.turret != null;
        
        if (settings.playerFactionOnly)
        {
            Faction playerFaction = Faction.OfPlayer;
            if (isTurret && __instance.turret.Faction != playerFaction) return true;
            if (!isTurret && __instance.Wielder?.Faction != playerFaction) return true;
        }
        
        if (isTurret && settings.infiniteTurretAmmo && __instance.HasMagazine)
        {
            __instance.CurMagCount = __instance.MagSize;
            return false;
        }
        
        if (!isTurret && settings.infiniteAmmo && __instance.HasMagazine)
        {
            AmmoDef selectedAmmo = __instance.SelectedAmmo;
            if (selectedAmmo != null && AmmoInventoryHelper.HasSpecificAmmoInInventory(__instance, selectedAmmo))
            {
                __instance.CurrentAmmo = selectedAmmo;
                __instance.CurMagCount = __instance.MagSize;
                return false;
            }
        }
        
        return true;
    }
}

[HarmonyPatch(typeof(CompAmmoUser), "get_HasAmmo")]
public static class Patch_CompAmmoUser_HasAmmo
{
    public static void Postfix(CompAmmoUser __instance, ref bool __result)
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings == null) return;
        
        bool isTurret = __instance.turret != null;
        
        if (settings.playerFactionOnly)
        {
            Faction playerFaction = Faction.OfPlayer;
            if (isTurret && __instance.turret.Faction != playerFaction) return;
            if (!isTurret && __instance.Wielder?.Faction != playerFaction) return;
        }
        
        if (isTurret && settings.infiniteTurretAmmo)
        {
            __result = true;
            return;
        }
        
        if (!isTurret && settings.infiniteAmmo)
        {
            AmmoDef currentAmmo = __instance.CurrentAmmo;
            if (currentAmmo != null && AmmoInventoryHelper.HasSpecificAmmoInInventory(__instance, currentAmmo))
            {
                __result = true;
            }
            else if (currentAmmo == null && AmmoInventoryHelper.HasAnyAmmoInInventory(__instance))
            {
                __result = true;
            }
        }
    }
}

[HarmonyPatch(typeof(CompAmmoUser), nameof(CompAmmoUser.LoadAmmo))]
public static class Patch_CompAmmoUser_LoadAmmo
{
    public static bool Prefix(CompAmmoUser __instance, Thing ammo, bool emptyMag)
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings == null) return true;
        
        bool isTurret = __instance.turret != null;
        
        if (settings.playerFactionOnly)
        {
            Faction playerFaction = Faction.OfPlayer;
            if (isTurret && __instance.turret.Faction != playerFaction) return true;
            if (!isTurret && __instance.Wielder?.Faction != playerFaction) return true;
        }
        
        bool useNoConsume = !isTurret && (settings.infiniteReserve || settings.infiniteAmmo);
        if (!useNoConsume || !__instance.UseAmmo) return true;
        
        AmmoDef ammoToLoad = null;
        
        if (ammo != null)
        {
            ammoToLoad = ammo.def as AmmoDef;
        }
        else
        {
            AmmoDef selectedAmmo = __instance.SelectedAmmo;
            if (selectedAmmo != null && AmmoInventoryHelper.HasSpecificAmmoInInventory(__instance, selectedAmmo))
            {
                ammoToLoad = selectedAmmo;
            }
            else
            {
                if (__instance.TryFindAmmoInInventory(out Thing foundAmmo))
                {
                    ammoToLoad = foundAmmo.def as AmmoDef;
                }
            }
        }
        
        if (ammoToLoad == null) return true;
        
        __instance.CurrentAmmo = ammoToLoad;
        
        int newMagCount;
        if (__instance.Props.reloadOneAtATime)
        {
            newMagCount = __instance.CurMagCount + __instance.CurAmmoCount;
            if (newMagCount > __instance.MagSize) newMagCount = __instance.MagSize;
        }
        else
        {
            newMagCount = __instance.MagSize;
        }
        
        __instance.CurMagCount = newMagCount;
        
        if (__instance.turret != null)
        {
            __instance.turret.SetReloading(false);
        }
        
        return false;
    }
}

[HarmonyPatch(typeof(CompAmmoUser), nameof(CompAmmoUser.TryUnload), new[] { typeof(Thing), typeof(bool) }, new[] { ArgumentType.Out, ArgumentType.Normal })]
public static class Patch_CompAmmoUser_TryUnload
{
    public static bool Prefix(CompAmmoUser __instance, out Thing droppedAmmo, bool forceUnload, ref bool __result)
    {
        droppedAmmo = null;
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings == null) return true;
        
        bool isTurret = __instance.turret != null;
        
        if (settings.playerFactionOnly)
        {
            Faction playerFaction = Faction.OfPlayer;
            if (isTurret && __instance.turret.Faction != playerFaction) return true;
            if (!isTurret && __instance.Wielder?.Faction != playerFaction) return true;
        }
        
        if (!isTurret && (settings.infiniteReserve || settings.infiniteAmmo))
        {
            __instance.CurMagCount = 0;
            __result = true;
            return false;
        }
        
        return true;
    }
}

[HarmonyPatch(typeof(CompAmmoUser), "get_MagSize")]
public static class Patch_CompAmmoUser_MagSize
{
    public static bool Prefix(CompAmmoUser __instance, ref int __result)
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings == null) return true;
        
        bool isTurret = __instance.turret != null;
        
        if (settings.playerFactionOnly)
        {
            if (isTurret && __instance.turret.Faction != Faction.OfPlayer) return true;
        }
        
        if (isTurret && settings.infiniteTurretAmmo)
        {
            __result = 1;
            return false;
        }
        
        return true;
    }
}
