using HarmonyLib;
using Verse;

namespace CombatExtendedInfiniteAmmo.Patches;

/// <summary>
/// Makes ammo items always display using the full-stack texture,
/// even when the actual stackCount is 1 (due to balance mode limiting stacks).
/// RimWorld's Graphic_StackCount selects sub-graphics based on stackCount / stackLimit ratio.
/// By reporting a high stackCount, we force it to pick the fullest visual.
/// </summary>
[HarmonyPatch(typeof(Graphic_StackCount), nameof(Graphic_StackCount.SubGraphicForStackCount))]
public static class Patch_Graphic_StackCount_SubGraphicForStackCount
{
    public static void Prefix(ref int stackCount, ThingDef def)
    {
        // Only apply when balance mode with stack limiting is active
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings is not { enableBalancing: true } || !settings.limitAmmoStackSize)
            return;
        
        if (def == null) return;
        
        int stackLimit = def.stackLimit;
        
        // When stackLimit is 1 (which we set for ammo), force full-stack appearance.
        // Non-ammo items with natural stackLimit of 1 (weapons, apparel) don't use Graphic_StackCount.
        if (stackLimit <= 1)
        {
            // Set stackCount high to trigger the highest sub-graphic tier
            stackCount = 75;
        }
    }
}
