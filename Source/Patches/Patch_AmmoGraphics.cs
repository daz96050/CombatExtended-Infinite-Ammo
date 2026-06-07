using HarmonyLib;
using Verse;
using CombatExtended;

namespace CombatExtendedInfiniteAmmo.Patches;

/// <summary>
/// Makes ammo items always display using the full-stack texture,
/// even when the actual stackCount is 1 (due to balance mode limiting stacks).
/// RimWorld's Graphic_StackCount selects sub-graphics based on stackCount thresholds.
/// By reporting a high stackCount, we force it to pick the fullest visual.
/// 
/// Excludes throwable/individual ammo types (grenades, boulders, mortar shells)
/// which should display as single items rather than a pile.
/// </summary>
[HarmonyPatch(typeof(Graphic_StackCount), nameof(Graphic_StackCount.SubGraphicForStackCount))]
public static class Patch_Graphic_StackCount_SubGraphicForStackCount
{
    // Ammo with original stack limits at or above this threshold is considered
    // "bulk/boxed" ammo and gets forced to full-stack appearance.
    // Below this threshold, items are individual units (grenades, rocks, shells).
    private const int BulkAmmoStackThreshold = 50;
    
    public static void Prefix(ref int stackCount, ThingDef def)
    {
        // Only apply when balance mode with stack limiting is active
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings is not { enableBalancing: true } || !settings.limitAmmoStackSize)
            return;
        
        if (def == null) return;
        
        // Only apply to AmmoDef items (those are the ones we modified)
        if (def is not AmmoDef) return;
        
        int stackLimit = def.stackLimit;
        
        // Only relevant when stackLimit has been forced to 1
        if (stackLimit > 1) return;
        
        // Check the original stack limit to determine if this is bulk ammo or individual items.
        // Bulk ammo (bullets, cartridges) originally stacks high and should show as a full box.
        // Individual items (grenades, boulders, mortar rounds) have lower original limits
        // and should display as a single item, not a pile.
        int originalLimit = AmmoBalanceManager.GetOriginalStackLimit(def);
        if (originalLimit < BulkAmmoStackThreshold) return;
        
        // Set stackCount high to trigger the highest sub-graphic tier
        stackCount = originalLimit;
    }
}
