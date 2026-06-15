using HarmonyLib;
using Verse;
using RimWorld;
using CombatExtended;
using System.Collections.Generic;
using System.Reflection;

namespace CombatExtendedInfiniteAmmo.Patches;

/// <summary>
/// Patches quest reward generation to prevent offering absurd quantities of ammo
/// when balance mode limits stack size to 1.
/// Without this patch, a quest might offer "40x ammo" which becomes 40 individual stacks.
/// </summary>
[HarmonyPatch]
public static class Patch_QuestRewards_InitFromValue
{
    private const int MaxAmmoPerReward = 3;
    
    public static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(Reward_Items), "InitFromValue");
    }
    
    public static void Postfix(Reward_Items __instance)
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings is not { enableBalancing: true } || !settings.limitAmmoStackSize)
            return;
        
        var items = Traverse.Create(__instance).Field("items").GetValue<List<Thing>>();
        if (items == null || items.Count == 0)
            return;
        
        CapAmmoInItemList(items);
    }
    
    internal static void CapAmmoInItemList(List<Thing> items)
    {
        // First, cap individual ammo stack counts to 1 (matching stackLimit)
        foreach (var thing in items)
        {
            if (thing?.def is AmmoDef && thing.stackCount > 1)
            {
                thing.stackCount = 1;
            }
        }
        
        // Then, limit total number of ammo stacks per type to MaxAmmoPerReward
        var ammoCounts = new Dictionary<ThingDef, int>();
        var toRemove = new List<Thing>();
        
        foreach (var thing in items)
        {
            if (thing?.def is not AmmoDef)
                continue;
            
            ammoCounts.TryGetValue(thing.def, out int count);
            ammoCounts[thing.def] = count + 1;
            
            if (count + 1 > MaxAmmoPerReward)
            {
                toRemove.Add(thing);
            }
        }
        
        foreach (var thing in toRemove)
        {
            items.Remove(thing);
            thing.Destroy();
        }
    }
}

/// <summary>
/// Patches Reward_Items.GenerateQuestParts as a fallback to catch any ammo rewards
/// that weren't generated via InitFromValue (e.g., scripted quest rewards).
/// This runs when quest parts are being assembled.
/// </summary>
[HarmonyPatch]
public static class Patch_QuestRewards_GenerateQuestParts
{
    public static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(Reward_Items), "GenerateQuestParts");
    }
    
    public static void Prefix(Reward_Items __instance)
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings is not { enableBalancing: true } || !settings.limitAmmoStackSize)
            return;
        
        var items = Traverse.Create(__instance).Field("items").GetValue<List<Thing>>();
        if (items == null || items.Count == 0)
            return;
        
        Patch_QuestRewards_InitFromValue.CapAmmoInItemList(items);
    }
}

/// <summary>
/// Patches ThingSetMaker.Generate to cap ammo quantities in any generated item set.
/// This provides broader coverage for things like caravan request rewards,
/// trade caravans offering ammo, or any other system using ThingSetMaker.
/// </summary>
[HarmonyPatch(typeof(ThingSetMaker), nameof(ThingSetMaker.Generate), new[] { typeof(ThingSetMakerParams) })]
public static class Patch_ThingSetMaker_Generate
{
    private const int MaxAmmoPerSet = 5;
    
    public static void Postfix(List<Thing> __result)
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings is not { enableBalancing: true } || !settings.limitAmmoStackSize)
            return;
        
        if (__result == null || __result.Count == 0)
            return;
        
        // Cap individual ammo stack counts
        foreach (var thing in __result)
        {
            if (thing?.def is AmmoDef && thing.stackCount > 1)
            {
                thing.stackCount = 1;
            }
        }
        
        // Limit total ammo stacks per type
        var ammoCounts = new Dictionary<ThingDef, int>();
        var toRemove = new List<Thing>();
        
        foreach (var thing in __result)
        {
            if (thing?.def is not AmmoDef)
                continue;
            
            ammoCounts.TryGetValue(thing.def, out int count);
            ammoCounts[thing.def] = count + 1;
            
            if (count + 1 > MaxAmmoPerSet)
            {
                toRemove.Add(thing);
            }
        }
        
        foreach (var thing in toRemove)
        {
            __result.Remove(thing);
            thing.Destroy();
        }
    }
}



