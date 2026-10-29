using HarmonyLib;
using Verse;
using RimWorld;
using System.Collections.Generic;

namespace CombatExtendedInfiniteAmmo.Patches;

/// <summary>
/// Patches quest reward generation to prevent offering absurd quantities of ammo
/// when balance mode limits stack size to 1.
/// Without this patch, a quest might offer "40x ammo" which becomes 40 individual stacks.
/// </summary>
[HarmonyPatch(typeof(Reward_Items), nameof(Reward_Items.InitFromValue))]
public static class Patch_QuestRewards_InitFromValue
{
    internal const int MaxAmmoPerReward = 3;

    internal static bool Active =>
        CombatExtendedInfiniteAmmoMod.Settings is { enableBalancing: true, limitAmmoStackSize: true };

    public static void Postfix(Reward_Items __instance)
    {
        if (!Active) return;
        AmmoStripUtility.CapAmmoInList(__instance.ItemsListForReading, MaxAmmoPerReward);
    }
}

/// <summary>
/// Fallback for ammo rewards that weren't generated via InitFromValue (e.g., scripted quest rewards).
/// Runs when the quest parts are assembled.
/// </summary>
[HarmonyPatch(typeof(Reward_Items), nameof(Reward_Items.GenerateQuestParts))]
public static class Patch_QuestRewards_GenerateQuestParts
{
    public static void Prefix(Reward_Items __instance)
    {
        if (!Patch_QuestRewards_InitFromValue.Active) return;
        AmmoStripUtility.CapAmmoInList(__instance.ItemsListForReading, Patch_QuestRewards_InitFromValue.MaxAmmoPerReward);
    }
}

/// <summary>
/// Caps ammo in any generated item set: trader stock (settlements, orbital traders, caravans),
/// caravan request rewards, map loot, and anything else built through ThingSetMaker.
/// </summary>
[HarmonyPatch(typeof(ThingSetMaker), nameof(ThingSetMaker.Generate), new[] { typeof(ThingSetMakerParams) })]
public static class Patch_ThingSetMaker_Generate
{
    private const int MaxAmmoPerSet = 5;

    public static void Postfix(List<Thing> __result)
    {
        if (!Patch_QuestRewards_InitFromValue.Active) return;
        AmmoStripUtility.CapAmmoInList(__result, MaxAmmoPerSet);
    }
}
