using HarmonyLib;
using Verse;
using RimWorld;
using CombatExtended;
using System.Collections.Generic;

namespace CombatExtendedInfiniteAmmo.Patches;

/// <summary>
/// Prevents grenade/one-use weapon self-consumption and adds a simulated reload delay.
/// 
/// Normally after throwing a grenade, SelfConsume destroys it and the pawn re-equips
/// the next one from inventory (which takes time). When we block SelfConsume, we need
/// to add an artificial cooldown to prevent instant repeated throws.
/// </summary>
public static class GrenadeCooldownTracker
{
    // Pawns that just had SelfConsume blocked and need an extended cooldown
    private static readonly HashSet<int> _pendingCooldown = new HashSet<int>();
    
    // Simulated "grab next grenade" time in ticks (2 seconds)
    public const int GrenadeCooldownTicks = 120;
    
    public static void MarkForExtendedCooldown(Pawn pawn)
    {
        if (pawn != null)
            _pendingCooldown.Add(pawn.thingIDNumber);
    }
    
    public static bool ConsumeAndCheck(Pawn pawn)
    {
        if (pawn == null) return false;
        return _pendingCooldown.Remove(pawn.thingIDNumber);
    }
}

[HarmonyPatch(typeof(Verb_ShootCEOneUse), "SelfConsume")]
public static class Patch_Verb_ShootCEOneUse_SelfConsume
{
    public static bool Prefix(Verb_ShootCEOneUse __instance)
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings == null) return true;
        
        // Prevent consumption if infinite grenades OR infinite ammo is active
        if (!settings.infiniteGrenades && !settings.infiniteAmmo) 
            return true;
        
        Pawn casterPawn = __instance.CasterPawn;
        
        if (settings.playerFactionOnly)
        {
            if (casterPawn != null && casterPawn.Faction != Faction.OfPlayer)
                return true;
        }
        
        // Mark this pawn for extended cooldown to simulate reload time
        GrenadeCooldownTracker.MarkForExtendedCooldown(casterPawn);
        
        return false;
    }
}

[HarmonyPatch(typeof(Verb_ThrowGrenade), "SelfConsume")]
public static class Patch_Verb_ThrowGrenade_SelfConsume
{
    public static bool Prefix(Verb_ThrowGrenade __instance)
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings == null) return true;

        // Prevent consumption if infinite grenades OR infinite ammo is active
        if (!settings.infiniteGrenades && !settings.infiniteAmmo) 
            return true;

        Pawn casterPawn = __instance.CasterPawn;
        
        if (settings.playerFactionOnly)
        {
            if (casterPawn != null && casterPawn.Faction != Faction.OfPlayer)
                return true;
        }

        // Mark this pawn for extended cooldown to simulate reload time
        GrenadeCooldownTracker.MarkForExtendedCooldown(casterPawn);
        
        return false;
    }
}

/// <summary>
/// Extends the stance cooldown after a grenade throw to simulate the time
/// it would normally take to re-equip the next grenade from inventory.
/// </summary>
[HarmonyPatch(typeof(Pawn_StanceTracker), nameof(Pawn_StanceTracker.SetStance))]
public static class Patch_Pawn_StanceTracker_SetStance
{
    public static void Prefix(Pawn_StanceTracker __instance, Stance newStance)
    {
        if (newStance is not Stance_Cooldown) return;
        
        Pawn pawn = __instance.pawn;
        if (!GrenadeCooldownTracker.ConsumeAndCheck(pawn)) return;
        
        // Extend the cooldown to simulate grenade reload time
        var busy = (Stance_Busy)newStance;
        if (busy.ticksLeft < GrenadeCooldownTracker.GrenadeCooldownTicks)
        {
            busy.ticksLeft = GrenadeCooldownTracker.GrenadeCooldownTicks;
        }
    }
}
