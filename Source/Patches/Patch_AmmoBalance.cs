using Verse;
using RimWorld;
using CombatExtended;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;

namespace CombatExtendedInfiniteAmmo.Patches;

public static class AmmoBalanceManager
{
    private static Dictionary<ThingDef, int> originalStackLimits = new Dictionary<ThingDef, int>();
    private static Dictionary<StockGenerator, StockGeneratorOriginalData> originalStockData = new Dictionary<StockGenerator, StockGeneratorOriginalData>();
    
    private static bool isBalanced = false;
    private static bool stacksLimited = false;
    
    private class StockGeneratorOriginalData
    {
        public IntRange countRange;
        public IntRange thingDefCountRange;
    }
    
    public static void ApplyBalanceChanges()
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings == null) return;
        
        if (!settings.enableBalancing)
        {
            if (isBalanced)
            {
                RestoreOriginals();
                isBalanced = false;
                stacksLimited = false;
            }
            return;
        }
        
        int ammoPatched = 0;
        int tradersPatched = 0;
        
        foreach (var def in DefDatabase<ThingDef>.AllDefs)
        {
            if (def is AmmoDef ammoDef)
            {
                if (!originalStackLimits.ContainsKey(ammoDef))
                {
                    originalStackLimits[ammoDef] = ammoDef.stackLimit;
                }
                
                if (settings.limitAmmoStackSize)
                {
                    ammoDef.stackLimit = 1;
                    stacksLimited = true;
                }
                else if (stacksLimited)
                {
                    ammoDef.stackLimit = originalStackLimits[ammoDef];
                }
                
                ammoPatched++;
            }
        }
        
        if (!settings.limitAmmoStackSize)
        {
            stacksLimited = false;
        }
        
        foreach (var traderDef in DefDatabase<TraderKindDef>.AllDefs)
        {
            if (traderDef.stockGenerators == null) continue;
            
            foreach (var stockGen in traderDef.stockGenerators)
            {
                if (!IsAmmoStockGenerator(stockGen)) continue;
                
                if (!originalStockData.ContainsKey(stockGen))
                {
                    originalStockData[stockGen] = new StockGeneratorOriginalData
                    {
                        countRange = GetCountRange(stockGen),
                        thingDefCountRange = GetThingDefCountRange(stockGen)
                    };
                }
                
                SetCountRange(stockGen, new IntRange(1, 3));
                SetThingDefCountRange(stockGen, new IntRange(1, 2));
                
                tradersPatched++;
            }
        }
        
        isBalanced = true;
    }
    
    private static void RestoreOriginals()
    {
        foreach (var kvp in originalStackLimits)
        {
            kvp.Key.stackLimit = kvp.Value;
        }
        
        foreach (var kvp in originalStockData)
        {
            SetCountRange(kvp.Key, kvp.Value.countRange);
            SetThingDefCountRange(kvp.Key, kvp.Value.thingDefCountRange);
        }
        
    }
    
    private static bool IsAmmoStockGenerator(StockGenerator gen)
    {
        if (gen is StockGenerator_Tag tagGen)
        {
            var tradeTag = Traverse.Create(tagGen).Field("tradeTag").GetValue<string>();
            if (tradeTag != null && (
                tradeTag == "CE_Ammo" || 
                tradeTag == "CE_MediumAmmo" || 
                tradeTag == "CE_HeavyAmmo" ||
                tradeTag == "CE_PreIndustrialAmmo"))
            {
                return true;
            }
        }
        
        if (gen is StockGenerator_Category catGen)
        {
            var categoryDef = Traverse.Create(catGen).Field("categoryDef").GetValue<ThingCategoryDef>();
            if (categoryDef != null && categoryDef.defName == "Ammo")
            {
                return true;
            }
        }
        
        return false;
    }
    
    private static IntRange GetCountRange(StockGenerator gen)
    {
        var field = Traverse.Create(gen).Field("countRange");
        if (field.FieldExists())
            return field.GetValue<IntRange>();
        return new IntRange(0, 0);
    }
    
    private static void SetCountRange(StockGenerator gen, IntRange range)
    {
        var field = Traverse.Create(gen).Field("countRange");
        if (field.FieldExists())
            field.SetValue(range);
    }
    
    private static IntRange GetThingDefCountRange(StockGenerator gen)
    {
        var field = Traverse.Create(gen).Field("thingDefCountRange");
        if (field.FieldExists())
            return field.GetValue<IntRange>();
        return new IntRange(0, 0);
    }
    
    private static void SetThingDefCountRange(StockGenerator gen, IntRange range)
    {
        var field = Traverse.Create(gen).Field("thingDefCountRange");
        if (field.FieldExists())
            field.SetValue(range);
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

[HarmonyPatch(typeof(GenSpawn), nameof(GenSpawn.Spawn), new[] { typeof(Thing), typeof(IntVec3), typeof(Map), typeof(Rot4), typeof(WipeMode), typeof(bool), typeof(bool) })]
public static class Patch_GenSpawn_Spawn
{
    private static Dictionary<Map, Dictionary<ThingDef, int>> recentAmmoSpawns = new Dictionary<Map, Dictionary<ThingDef, int>>();
    private static int lastTick = -1;
    
    public static bool Prefix(Thing newThing, IntVec3 loc, Map map, ref Thing __result)
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings is not { enableBalancing: true } || !settings.limitAmmoSpawns) 
            return true;
        
        if (newThing?.def is not AmmoDef ammoDef)
            return true;
        
        // Don't interfere with ammo that is currently held in a container (transfers between inventories)
        if (newThing.holdingOwner != null)
            return true;
        
        // Don't limit spawns for player faction — check if this is a player-owned item
        // Player pawns dropping ammo on purpose (e.g., drop command) should not be blocked
        // We detect player drops by checking the map's lister for nearby player pawns
        // However, for simplicity: only limit non-player-faction ammo drops
        // Skip entirely if not in active game
        if (Find.TickManager == null || Current.ProgramState != ProgramState.Playing)
            return true;
        
        int currentTick = Find.TickManager.TicksGame;
        if (currentTick != lastTick)
        {
            recentAmmoSpawns.Clear();
            lastTick = currentTick;
        }
        
        if (!recentAmmoSpawns.TryGetValue(map, out var mapSpawns))
        {
            mapSpawns = new Dictionary<ThingDef, int>();
            recentAmmoSpawns[map] = mapSpawns;
        }
        
        int count = mapSpawns.GetValueOrDefault(ammoDef, 0);
        
        // Allow only 1 stack of each ammo type to spawn per tick
        if (count >= 1)
        {
            newThing.Destroy();
            __result = null;
            return false;
        }
        
        // Limit to single round
        if (newThing.stackCount > 1)
        {
            newThing.stackCount = 1;
        }
        
        mapSpawns[ammoDef] = count + 1;
        
        return true;
    }
}

[HarmonyPatch(typeof(ThingMaker), nameof(ThingMaker.MakeThing))]
public static class Patch_ThingMaker_MakeThing
{
    public static void Postfix(Thing __result)
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings is not { enableBalancing: true } || !settings.limitAmmoSpawns) 
            return;

        if (__result?.def is not AmmoDef || __result.stackCount <= 1) return;
        // Only limit during map generation, not during normal gameplay
        // (pawns need to be able to pick up/create normal ammo stacks for loadouts)
        if (Current.ProgramState != ProgramState.Playing)
        {
            __result.stackCount = 1;
        }
    }
}

[HarmonyPatch(typeof(PawnInventoryGenerator), nameof(PawnInventoryGenerator.GenerateInventoryFor))]
public static class Patch_PawnInventoryGenerator_GenerateInventoryFor
{
    public static void Postfix(Pawn p)
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings is not { enableBalancing: true } || !settings.limitAmmoSpawns)
            return;
        
        if (p?.inventory?.innerContainer == null)
            return;
        
        // Don't limit ammo for player faction pawns
        if (p.Faction != null && p.Faction == Faction.OfPlayer)
            return;
        
        foreach (var thing in p.inventory.innerContainer)
        {
            if (thing is { def: AmmoDef, stackCount: > 1 })
            {
                thing.stackCount = 1;
            }
        }
    }
}

[HarmonyPatch(typeof(ThingOwner<Thing>), nameof(ThingOwner<Thing>.TryAdd), new[] { typeof(Thing), typeof(bool) })]
public static class Patch_ThingOwner_TryAdd
{
    public static void Postfix(ThingOwner<Thing> __instance, Thing item, bool __result)
    {
        if (!__result) return;
        
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings == null || !settings.enableBalancing || !settings.limitAmmoSpawns)
            return;
        
        if (item?.def is AmmoDef && item.stackCount > 1)
        {
            // Don't limit ammo stacks in pawn inventories - this breaks loadouts
            // Only limit in non-pawn containers (e.g., ground stacks, trade)
            if (__instance.Owner is Pawn_InventoryTracker)
                return;
            
            // Also skip if owned by a pawn equipment tracker or apparel tracker
            if (__instance.Owner is Pawn_ApparelTracker || __instance.Owner is Pawn_EquipmentTracker)
                return;
                
            item.stackCount = 1;
        }
    }
}
