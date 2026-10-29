using Verse;
using RimWorld;
using CombatExtended;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CombatExtendedInfiniteAmmo.Patches;

/// <summary>
/// Decides which ammo types the rebalance applies to and applies the stack limit.
/// An ammo type is only rebalanced when something that fires it actually gets infinite ammo,
/// so e.g. mortar shells keep their normal cost and stack size while infinite mortar ammo is off.
/// </summary>
public static class AmmoBalanceManager
{
    [Flags]
    private enum AmmoUsers
    {
        None = 0,
        Pawn = 1,
        Turret = 2,
        Mortar = 4,
        Grenade = 8
    }

    private static Dictionary<AmmoDef, AmmoUsers> ammoUsers;
    private static readonly Dictionary<AmmoDef, int> originalStackLimits = new Dictionary<AmmoDef, int>();
    private static readonly HashSet<ThingDef> balancedAmmo = new HashSet<ThingDef>();

    /// <summary>
    /// True if balancing is enabled and applies to this def. Cheap enough for hot paths.
    /// </summary>
    public static bool IsBalanced(ThingDef def)
    {
        return def != null && balancedAmmo.Contains(def);
    }

    public static void ApplyBalanceChanges()
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings == null) return;

        ammoUsers ??= ClassifyAmmo();
        balancedAmmo.Clear();

        foreach (var kvp in ammoUsers)
        {
            AmmoDef ammoDef = kvp.Key;
            if (!originalStackLimits.ContainsKey(ammoDef))
            {
                originalStackLimits[ammoDef] = ammoDef.stackLimit;
            }

            bool balanced = settings.enableBalancing && IsInfiniteFor(kvp.Value, settings);
            if (balanced) balancedAmmo.Add(ammoDef);

            ammoDef.stackLimit = balanced && settings.limitAmmoStackSize ? 1 : originalStackLimits[ammoDef];
        }

        AmmoRecipeManager.ApplyRecipeChanges();
    }

    private static bool IsInfiniteFor(AmmoUsers users, InfiniteAmmoSettings settings)
    {
        // Ammo nothing is known to fire is treated as small-arms ammo
        if (users == AmmoUsers.None) users = AmmoUsers.Pawn;

        return ((users & AmmoUsers.Pawn) != 0 && settings.ammoMode != InfiniteAmmoMode.Off) ||
               ((users & AmmoUsers.Turret) != 0 && settings.infiniteTurretAmmo) ||
               ((users & AmmoUsers.Mortar) != 0 && settings.infiniteMortarAmmo) ||
               ((users & AmmoUsers.Grenade) != 0 && (settings.infiniteGrenades || settings.infiniteAmmo));
    }

    private static Dictionary<AmmoDef, AmmoUsers> ClassifyAmmo()
    {
        var result = new Dictionary<AmmoDef, AmmoUsers>();

        // Turret guns are separate ThingDefs referenced by their turret building
        var turretGuns = new Dictionary<ThingDef, AmmoUsers>();
        foreach (var def in DefDatabase<ThingDef>.AllDefs)
        {
            ThingDef gun = def.building?.turretGunDef;
            if (gun == null) continue;
            turretGuns.TryGetValue(gun, out AmmoUsers kind);
            turretGuns[gun] = kind | (def.building.IsMortar ? AmmoUsers.Mortar : AmmoUsers.Turret);
        }

        foreach (var def in DefDatabase<ThingDef>.AllDefs)
        {
            if (def is AmmoDef ammoDef)
            {
                result.TryGetValue(ammoDef, out AmmoUsers own);
                // Grenades and other thrown weapons are their own ammo
                result[ammoDef] = def.IsWeapon ? own | AmmoUsers.Grenade : own;
            }

            var ammoTypes = def.GetCompProperties<CompProperties_AmmoUser>()?.ammoSet?.ammoTypes;
            if (ammoTypes == null) continue;

            AmmoUsers user = turretGuns.TryGetValue(def, out AmmoUsers turretKind) ? turretKind : AmmoUsers.Pawn;
            foreach (var link in ammoTypes)
            {
                if (link?.ammo == null) continue;
                result.TryGetValue(link.ammo, out AmmoUsers existing);
                result[link.ammo] = existing | user;
            }
        }

        return result;
    }
}

[HarmonyPatch(typeof(AmmoInjector), nameof(AmmoInjector.Inject))]
public static class Patch_AmmoInjector_ApplyBalance
{
    public static void Postfix()
    {
        AmmoBalanceManager.ApplyBalanceChanges();
    }
}

/// <summary>
/// Shared helpers for capping ammo in pawn inventories and generated item lists.
/// </summary>
public static class AmmoStripUtility
{
    private static bool SpawnLimitActive =>
        CombatExtendedInfiniteAmmoMod.Settings is { enableBalancing: true, limitAmmoSpawns: true };

    /// <summary>
    /// Keeps a single round of each rebalanced ammo type in the pawn's inventory and destroys the rest.
    /// </summary>
    public static void StripExcessAmmo(Pawn pawn, bool includePlayerPawns)
    {
        if (!SpawnLimitActive || pawn == null) return;
        if (!includePlayerPawns && pawn.Faction != null && pawn.Faction == Faction.OfPlayer) return;

        var container = pawn.inventory?.innerContainer;
        if (container == null || container.Count == 0) return;

        var seen = new HashSet<ThingDef>();
        for (int i = container.Count - 1; i >= 0; i--)
        {
            Thing thing = container[i];
            if (!AmmoBalanceManager.IsBalanced(thing?.def)) continue;

            if (seen.Add(thing.def))
            {
                if (thing.stackCount > 1) thing.stackCount = 1;
            }
            else
            {
                container.Remove(thing);
                thing.Destroy();
            }
        }
    }

    /// <summary>
    /// Caps every rebalanced ammo stack in the list to 1 and keeps at most maxPerType stacks of each type.
    /// </summary>
    public static void CapAmmoInList(List<Thing> things, int maxPerType)
    {
        if (things == null || things.Count == 0) return;

        Dictionary<ThingDef, int> counts = null;
        for (int i = 0; i < things.Count; i++)
        {
            Thing thing = things[i];
            if (!AmmoBalanceManager.IsBalanced(thing?.def)) continue;

            counts ??= new Dictionary<ThingDef, int>();
            counts.TryGetValue(thing.def, out int count);
            if (count >= maxPerType)
            {
                things.RemoveAt(i--);
                thing.Destroy();
                continue;
            }

            counts[thing.def] = count + 1;
            if (thing.stackCount > 1) thing.stackCount = 1;
        }
    }
}

/// <summary>
/// With stack limiting on, clamps ammo spawned with more than one round the way vanilla
/// would anyway, without the "stackCount > stackLimit" error. GenPlace already splits
/// oversized stacks before spawning, so this only catches direct GenSpawn callers.
/// </summary>
[HarmonyPatch(typeof(GenSpawn), nameof(GenSpawn.Spawn), new[] { typeof(Thing), typeof(IntVec3), typeof(Map), typeof(Rot4), typeof(WipeMode), typeof(bool), typeof(bool) })]
public static class Patch_GenSpawn_Spawn
{
    public static void Prefix(Thing newThing)
    {
        if (newThing == null || newThing.stackCount <= 1) return;

        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings is not { enableBalancing: true, limitAmmoStackSize: true }) return;

        if (AmmoBalanceManager.IsBalanced(newThing.def))
        {
            newThing.stackCount = 1;
        }
    }
}

/// <summary>
/// Caps generated pawns (raiders, visitors, traders, new mechs, new colonists) to one round per ammo type.
/// GenerateGearFor is patched rather than GenerateInventoryFor because, in 1.6, CE adds its
/// ammo from a PawnWeaponGenerator postfix that runs after GenerateInventoryFor.
/// </summary>
[HarmonyPatch]
public static class Patch_PawnGenerator_GenerateGearFor
{
    public static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(PawnGenerator), "GenerateGearFor");
    }

    public static void Postfix(Pawn pawn)
    {
        AmmoStripUtility.StripExcessAmmo(pawn, includePlayerPawns: true);
    }
}

/// <summary>
/// Non-player pawns can pick up ammo during play, so trim it again when they die
/// (corpse inventory) or drop everything (downed, arrested, etc.).
/// </summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
public static class Patch_Pawn_Kill
{
    public static void Prefix(Pawn __instance)
    {
        AmmoStripUtility.StripExcessAmmo(__instance, includePlayerPawns: false);
    }
}

[HarmonyPatch(typeof(Pawn_InventoryTracker), nameof(Pawn_InventoryTracker.DropAllNearPawn))]
public static class Patch_DropAllNearPawn
{
    public static void Prefix(Pawn_InventoryTracker __instance)
    {
        AmmoStripUtility.StripExcessAmmo(__instance.pawn, includePlayerPawns: false);
    }
}
