using HarmonyLib;
using Verse;
using RimWorld;
using CombatExtended;
using System;

namespace CombatExtendedInfiniteAmmo.Patches;

[HarmonyPatch(typeof(Verb_ShootCEOneUse), "SelfConsume")]
public static class Patch_Verb_ShootCEOneUse_SelfConsume
{
    public static bool Prefix(Verb_ShootCEOneUse __instance)
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings == null || !settings.infiniteGrenades) 
            return true;
        
        if (settings.playerFactionOnly)
        {
            Pawn casterPawn = __instance.CasterPawn;
            if (casterPawn != null && casterPawn.Faction != Faction.OfPlayer)
                return true;
        }
        
        return false;
    }
}

[HarmonyPatch(typeof(Verb_ThrowGrenade), "SelfConsume")]
public static class Patch_Verb_ThrowGrenade_SelfConsume
{
    public static bool Prefix(Verb_ThrowGrenade __instance)
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings == null || !settings.infiniteGrenades) 
            return true;

        if (settings.playerFactionOnly)
        {
            Pawn casterPawn = __instance.CasterPawn;
            if (casterPawn != null && casterPawn.Faction != Faction.OfPlayer)
                return true;
        }

        return false;
    }
}
