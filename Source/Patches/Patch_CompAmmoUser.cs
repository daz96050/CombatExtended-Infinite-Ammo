using HarmonyLib;
using Verse;
using RimWorld;
using CombatExtended;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Verse.AI;
using Verse.Sound;

namespace CombatExtendedInfiniteAmmo.Patches;

/// <summary>
/// Eligibility checks shared by the CompAmmoUser patches.
/// Several of the patched members (MagSize, HasAmmo) are queried many times per tick,
/// so every check bails out on the cheapest condition first and avoids CE's Wielder
/// property, which does several GetComp lookups per call.
/// </summary>
public static class AmmoInventoryHelper
{
    private static readonly Dictionary<ThingDef, bool> mortarCache = new Dictionary<ThingDef, bool>();

    public static bool IsMortar(Building_Turret turret)
    {
        if (turret == null) return false;
        ThingDef def = turret.def;
        if (!mortarCache.TryGetValue(def, out bool isMortar))
        {
            isMortar = def.building != null && def.building.IsMortar;
            mortarCache[def] = isMortar;
        }
        return isMortar;
    }

    /// <summary>
    /// The pawn holding this weapon, either equipped or carried in inventory.
    /// </summary>
    public static Pawn HolderPawn(CompAmmoUser comp)
    {
        return comp.parent.ParentHolder switch
        {
            Pawn_EquipmentTracker equipment => equipment.pawn,
            Pawn_InventoryTracker inventory => inventory.pawn,
            _ => null
        };
    }

    private static bool FactionAllowed(Faction faction, InfiniteAmmoSettings settings)
    {
        return !settings.playerFactionOnly || (faction != null && faction == Faction.OfPlayer);
    }

    /// <summary>
    /// Check if the turret qualifies for infinite turret ammo (excludes mortars).
    /// </summary>
    public static bool IsInfiniteTurret(CompAmmoUser comp, InfiniteAmmoSettings settings)
    {
        if (comp.turret == null || !settings.infiniteTurretAmmo) return false;
        return !IsMortar(comp.turret) && FactionAllowed(comp.turret.Faction, settings);
    }

    /// <summary>
    /// Check if the turret is a mortar that qualifies for infinite mortar ammo.
    /// </summary>
    public static bool IsInfiniteMortar(CompAmmoUser comp, InfiniteAmmoSettings settings)
    {
        if (comp.turret == null || !settings.infiniteMortarAmmo) return false;
        return IsMortar(comp.turret) && FactionAllowed(comp.turret.Faction, settings);
    }

    /// <summary>
    /// Check if this is a pawn-held weapon covered by Infinite Ammo or Infinite Reserve.
    /// </summary>
    public static bool IsInfinitePawn(CompAmmoUser comp, InfiniteAmmoSettings settings)
    {
        if (comp.turret != null || settings.ammoMode == InfiniteAmmoMode.Off) return false;
        if (!settings.playerFactionOnly) return true;
        return FactionAllowed(HolderPawn(comp)?.Faction, settings);
    }

    /// <summary>
    /// Pawn weapon that reloads from nothing: the selected ammo type is loaded
    /// without any of it needing to be in the inventory.
    /// </summary>
    public static bool IsNoAmmoRequired(CompAmmoUser comp, InfiniteAmmoSettings settings)
    {
        return !settings.requireAmmoInInventory && IsInfinitePawn(comp, settings);
    }
}

[HarmonyPatch(typeof(CompAmmoUser), nameof(CompAmmoUser.Notify_ShotFired))]
public static class Patch_CompAmmoUser_Notify_ShotFired
{
    public static bool Prefix(CompAmmoUser __instance)
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings == null) return true;

        if (__instance.turret != null)
        {
            // Infinite turret / mortar: never deplete (mortar ammo was consumed on load)
            bool infinite = AmmoInventoryHelper.IsInfiniteTurret(__instance, settings) ||
                            AmmoInventoryHelper.IsInfiniteMortar(__instance, settings);
            return !(infinite && __instance.CurMagCount > 0);
        }

        // Infinite ammo for pawns: never deplete mag
        return !(settings.infiniteAmmo && AmmoInventoryHelper.IsInfinitePawn(__instance, settings));
    }
}

/// <summary>
/// Handles reloads for infinite turrets, mortars and pawn weapons without going through CE's
/// HasAmmo checks. On RimWorld 1.7 the JIT may inline CE's small getters (HasAmmo, MagSize) into
/// their callers, which bypasses the getter patches; CE then thinks a no-ammo pawn is out of
/// ammo, empties the magazine and runs its out-of-ammo action (switching away from the weapon).
/// TryStartReload is large enough that its own patch is reliable.
/// </summary>
[HarmonyPatch(typeof(CompAmmoUser), nameof(CompAmmoUser.TryStartReload))]
public static class Patch_CompAmmoUser_TryStartReload
{
    private static readonly FieldInfo RgStanceField = AccessTools.Field(typeof(CompAmmoUser), "rgStance");
    private static readonly Type TacticalDriverType = AccessTools.TypeByName("CombatExtended.AI.IJobDriver_Tactical");

    public static bool Prefix(CompAmmoUser __instance)
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings == null) return true;

        return __instance.turret == null ? PawnPrefix(__instance, settings) : TurretPrefix(__instance, settings);
    }

    private static bool PawnPrefix(CompAmmoUser comp, InfiniteAmmoSettings settings)
    {
        if (!AmmoInventoryHelper.IsInfinitePawn(comp, settings) || !comp.UseAmmo) return true;

        Pawn pawn = comp.Wielder;
        if (pawn == null || (TacticalDriverType != null && TacticalDriverType.IsInstanceOfType(pawn.jobs?.curDriver)))
            return true;

        // Truly out of ammo: let CE pick some up or switch weapons
        Thing found = null;
        if (!AmmoInventoryHelper.IsNoAmmoRequired(comp, settings) && !comp.TryFindAmmoInInventory(out found))
            return true;

        // Magazine-less weapons (bows, etc.) have nothing to reload while they have ammo
        if (!comp.HasMagazine) return false;

        // Infinite Ammo (no reloading): switch to the selected type instantly
        if (settings.infiniteAmmo)
        {
            comp.CurrentAmmo = found?.def as AmmoDef ?? comp.SelectedAmmo ?? comp.CurrentAmmo;
            comp.CurMagCount = comp.MagSize;
            return false;
        }

        // Infinite Reserve: CE's own flow, minus its HasAmmo check
        if (RgStanceField?.GetValue(null) is Type rgStance && pawn.stances.curStance.GetType() == rgStance)
            return false;

        comp.TryUnload();

        if (comp.Props.reloadOneAtATime && comp.SelectedAmmo == comp.CurrentAmmo && comp.CurMagCount == comp.MagSize)
            return false;
        if (pawn.CurJobDef == CE_JobDefOf.ReloadWeapon) return false;

        Job job = comp.TryMakeReloadJob();
        if (job == null) return false;

        job.playerForced = true;
        pawn.jobs.StartJob(job, JobCondition.InterruptForced, resumeCurJobAfterwards: pawn.CurJob?.def != job.def);
        return false;
    }

    private static bool TurretPrefix(CompAmmoUser __instance, InfiniteAmmoSettings settings)
    {
        // Infinite turret (non-mortar): instant refill
        if (AmmoInventoryHelper.IsInfiniteTurret(__instance, settings) && __instance.HasMagazine)
        {
            __instance.CurMagCount = __instance.MagSize;
            return false;
        }

        // Infinite mortar: if same ammo type selected, instant refill.
        // Different ammo type: let normal reload happen (unloads old, loads new - both consume/return normally)
        if (AmmoInventoryHelper.IsInfiniteMortar(__instance, settings) && __instance.HasMagazine)
        {
            AmmoDef selected = __instance.SelectedAmmo;
            if (selected != null && selected != __instance.CurrentAmmo) return true;
            __instance.CurMagCount = __instance.MagSize;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(CompAmmoUser), nameof(CompAmmoUser.HasAmmo), MethodType.Getter)]
public static class Patch_CompAmmoUser_HasAmmo
{
    public static void Postfix(CompAmmoUser __instance, ref bool __result)
    {
        if (!__result && ForceHasAmmo(__instance)) __result = true;
    }

    internal static bool ForceHasAmmo(CompAmmoUser comp)
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings == null) return false;

        if (comp.turret != null)
        {
            // Mortar has infinite ammo of its currently loaded type
            return AmmoInventoryHelper.IsInfiniteTurret(comp, settings) ||
                   (AmmoInventoryHelper.IsInfiniteMortar(comp, settings) && comp.CurrentAmmo != null && comp.CurMagCount > 0);
        }

        // Pawns otherwise keep CE's answer (is there compatible ammo in the inventory?).
        // Forcing it true made pawns with no ammo retry a reload job that fails forever.
        return AmmoInventoryHelper.IsNoAmmoRequired(comp, settings);
    }
}

// HasAmmo may be inlined into these on 1.7, so they get the same treatment
[HarmonyPatch(typeof(CompAmmoUser), nameof(CompAmmoUser.HasAmmoOrMagazine), MethodType.Getter)]
public static class Patch_CompAmmoUser_HasAmmoOrMagazine
{
    public static void Postfix(CompAmmoUser __instance, ref bool __result)
    {
        if (!__result && Patch_CompAmmoUser_HasAmmo.ForceHasAmmo(__instance)) __result = true;
    }
}

[HarmonyPatch(typeof(CompAmmoUser), nameof(CompAmmoUser.CanBeFiredNow), MethodType.Getter)]
public static class Patch_CompAmmoUser_CanBeFiredNow
{
    public static void Postfix(CompAmmoUser __instance, ref bool __result)
    {
        // Magazine weapons must keep reporting an empty mag, or Infinite Reserve would never reload
        if (__result || __instance.turret != null) return;

        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings != null && AmmoInventoryHelper.IsNoAmmoRequired(__instance, settings) && !__instance.HasMagazine)
        {
            __result = true;
        }
    }
}

/// <summary>
/// Last line of defence against CE switching weapons, dropping its mag or hunting for ammo
/// when the weapon can keep firing under the current infinite mode.
/// </summary>
[HarmonyPatch(typeof(CompAmmoUser), "DoOutOfAmmoAction")]
public static class Patch_CompAmmoUser_DoOutOfAmmoAction
{
    public static bool Prefix(CompAmmoUser __instance)
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings == null || !AmmoInventoryHelper.IsInfinitePawn(__instance, settings)) return true;

        bool canKeepFiring = AmmoInventoryHelper.IsNoAmmoRequired(__instance, settings) ||
                             (settings.infiniteAmmo && __instance.HasMagazine && __instance.CurMagCount > 0);
        return !canKeepFiring;
    }
}

/// <summary>
/// The reload job fails itself when CE's HasAmmo says false, which on 1.7 can bypass the HasAmmo patch.
/// With no ammo required, only fail it when the weapon itself went away.
/// </summary>
[HarmonyPatch(typeof(CombatExtended.JobDriver_Reload), "HasNoGunOrAmmo")]
public static class Patch_JobDriver_Reload_HasNoGunOrAmmo
{
    public static void Postfix(CombatExtended.JobDriver_Reload __instance, ref bool __result,
        bool ___reloadingEquipment, bool ___reloadingInventory, ThingWithComps ___initEquipment)
    {
        if (!__result) return;

        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings == null || settings.requireAmmoInInventory) return;

        var weapon = __instance.job?.targetB.Thing as ThingWithComps;
        var comp = weapon?.TryGetComp<CompAmmoUser>();
        if (comp == null || !AmmoInventoryHelper.IsNoAmmoRequired(comp, settings)) return;

        Pawn pawn = __instance.pawn;
        Thing primary = pawn.equipment?.Primary;
        __result = (___reloadingEquipment && primary != weapon) ||
                   (___reloadingInventory && pawn.inventory?.innerContainer.Contains(weapon) != true) ||
                   ___initEquipment != primary;
    }
}

/// <summary>
/// With "require ammo in inventory" off, report the selected type as available without
/// touching the inventory. Returning a null Thing is handled by the LoadAmmo and
/// TryPrepareShot patches, which load SelectedAmmo instead. CE's own fallback would
/// switch SelectedAmmo to whatever type the pawn happens to carry, overriding the
/// player's choice.
/// </summary>
[HarmonyPatch(typeof(CompAmmoUser), nameof(CompAmmoUser.TryFindAmmoInInventory))]
public static class Patch_CompAmmoUser_TryFindAmmoInInventory
{
    public static bool Prefix(CompAmmoUser __instance, ref Thing ammoThing, ref bool __result)
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings == null || settings.requireAmmoInInventory) return true;
        if (!AmmoInventoryHelper.IsNoAmmoRequired(__instance, settings)) return true;

        ammoThing = null;
        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(CompAmmoUser), nameof(CompAmmoUser.LoadAmmo))]
public static class Patch_CompAmmoUser_LoadAmmo
{
    public static bool Prefix(CompAmmoUser __instance, Thing ammo)
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings == null) return true;

        // For mortars and turrets: let normal LoadAmmo run (consumes ammo on load/switch)
        if (!AmmoInventoryHelper.IsInfinitePawn(__instance, settings)) return true;
        if (!__instance.UseAmmo) return true;

        // For pawns with infinite ammo/reserve: don't consume inventory ammo
        AmmoDef ammoToLoad = ammo?.def as AmmoDef;
        if (ammoToLoad == null && __instance.TryFindAmmoInInventory(out Thing foundAmmo))
        {
            // foundAmmo is null when no ammo is required: load whatever is selected
            ammoToLoad = foundAmmo?.def as AmmoDef ?? __instance.SelectedAmmo ?? __instance.CurrentAmmo;
        }

        // Nothing to load: let CE run its out-of-ammo handling
        if (ammoToLoad == null) return true;

        // Set ammo type without consuming anything
        __instance.CurrentAmmo = ammoToLoad;

        // Fill the magazine
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

        Pawn holder = AmmoInventoryHelper.HolderPawn(__instance);
        if (__instance.parent.def.soundInteract != null && holder?.Map != null)
        {
            __instance.parent.def.soundInteract.PlayOneShot(new TargetInfo(holder.Position, holder.Map));
        }

        // Do NOT consume the ammo Thing - skip original method entirely
        return false;
    }
}

/// <summary>
/// Unloading a pawn's weapon empties the magazine without returning ammo to the inventory,
/// since loading it never consumed any. Mirrors CE's early-outs so that, e.g., a
/// reload-one-at-a-time shotgun topping up the same shell type keeps its loaded shells.
/// The bool-only overload forwards to this one.
/// </summary>
[HarmonyPatch(typeof(CompAmmoUser), nameof(CompAmmoUser.TryUnload),
    new[] { typeof(Thing), typeof(bool) }, new[] { ArgumentType.Out, ArgumentType.Normal })]
public static class Patch_CompAmmoUser_TryUnload
{
    public static bool Prefix(CompAmmoUser __instance, ref Thing droppedAmmo, bool forceUnload, ref bool __result)
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings == null) return true;

        // For mortars and turrets: let normal TryUnload run (returns ammo when switching types)
        if (!AmmoInventoryHelper.IsInfinitePawn(__instance, settings)) return true;

        // Cases where CE returns without unloading anything
        if (!__instance.UseAmmo || __instance.CurMagCount == 0 || !__instance.HasMagazine) return true;
        if (AmmoInventoryHelper.HolderPawn(__instance) == null) return true;
        if (__instance.Props.reloadOneAtATime && !forceUnload && __instance.SelectedAmmo == __instance.CurrentAmmo)
            return true;

        droppedAmmo = null;
        __instance.CurMagCount = 0;
        __result = true;
        return false;
    }
}

// Prevent ammo from being consumed from inventory when preparing a shot
[HarmonyPatch(typeof(CompAmmoUser), nameof(CompAmmoUser.TryPrepareShot))]
public static class Patch_CompAmmoUser_TryPrepareShot
{
    public static bool Prefix(CompAmmoUser __instance, ref bool __result)
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings == null) return true;

        if (__instance.turret != null)
        {
            if (!AmmoInventoryHelper.IsInfiniteTurret(__instance, settings) &&
                !AmmoInventoryHelper.IsInfiniteMortar(__instance, settings)) return true;
            __result = true;
            return false;
        }

        if (!AmmoInventoryHelper.IsInfinitePawn(__instance, settings)) return true;

        if (settings.infiniteAmmo)
        {
            __result = true;
            return false;
        }

        // Infinite reserve with a magazine: CE only checks the mag here, nothing to change
        if (__instance.HasMagazine) return true;

        // Infinite reserve without a magazine (bows, etc.): CE would split a round off the
        // inventory stack and destroy it after the shot. Fire the round without consuming it.
        if (!__instance.UseAmmo || !__instance.TryFindAmmoInInventory(out Thing ammo))
        {
            return true;
        }

        // ammo is null when no ammo is required; SelectedAmmo already drives CurrentAmmo for magazine-less weapons
        if (ammo?.def is AmmoDef ammoDef && ammoDef != __instance.CurrentAmmo)
        {
            __instance.CurrentAmmo = ammoDef;
        }
        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(CompAmmoUser), nameof(CompAmmoUser.MagSize), MethodType.Getter)]
public static class Patch_CompAmmoUser_MagSize
{
    public static bool Prefix(CompAmmoUser __instance, ref int __result)
    {
        // Hot path: queried constantly through HasMagazine. Non-turrets bail immediately.
        if (__instance.turret == null) return true;

        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings == null || !settings.infiniteTurretAmmo) return true;

        // Set mag size to 1 for infinite turrets (not mortars - mortars keep normal mag size)
        if (!AmmoInventoryHelper.IsInfiniteTurret(__instance, settings)) return true;
        __result = 1;
        return false;
    }
}

/// <summary>
/// With "require ammo in inventory" off, list every ammo type in the reload/ammo-select menu,
/// not just the ones carried. CE already does this in god mode; the menu's god mode check is
/// rewritten to also pass while this command's weapon needs no ammo, without touching the
/// global flag.
/// </summary>
[HarmonyPatch(typeof(Command_Reload), "BuildAmmoOptions")]
public static class Patch_Command_Reload_BuildAmmoOptions
{
    private static bool showAllAmmoTypes;

    public static void Prefix(Command_Reload __instance)
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        showAllAmmoTypes = settings is { requireAmmoInInventory: false } && __instance.compAmmo != null &&
                           AmmoInventoryHelper.IsNoAmmoRequired(__instance.compAmmo, settings);
    }

    public static void Finalizer()
    {
        showAllAmmoTypes = false;
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        FieldInfo godMode = AccessTools.Field(typeof(DebugSettings), nameof(DebugSettings.godMode));
        MethodInfo replacement = AccessTools.Method(typeof(Patch_Command_Reload_BuildAmmoOptions), nameof(ShowAllAmmoTypes));
        bool replaced = false;

        foreach (var instruction in instructions)
        {
            if (instruction.LoadsField(godMode))
            {
                replaced = true;
                yield return new CodeInstruction(OpCodes.Call, replacement).MoveLabelsFrom(instruction);
            }
            else
            {
                yield return instruction;
            }
        }

        if (!replaced)
            Log.Warning("[CE Infinite Ammo] Could not find CE's ammo menu filter; the menu will only list carried ammo.");
    }

    public static bool ShowAllAmmoTypes() => DebugSettings.godMode || showAllAmmoTypes;
}
