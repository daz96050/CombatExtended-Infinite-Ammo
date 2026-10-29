using Verse;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CombatExtendedInfiniteAmmo.Patches;

/// <summary>
/// Makes recipes for rebalanced ammo yield a single round at a multiplied cost.
/// Called from AmmoBalanceManager.ApplyBalanceChanges, after the set of rebalanced ammo is known.
/// </summary>
public static class AmmoRecipeManager
{
    private static readonly Dictionary<RecipeDef, RecipeOriginalData> originalData = new Dictionary<RecipeDef, RecipeOriginalData>();

    private class RecipeOriginalData
    {
        public int productCount;
        public float workAmount;
        public string label;
        public string description;
        public Dictionary<IngredientCount, float> ingredientCounts = new Dictionary<IngredientCount, float>();
    }

    public static void ApplyRecipeChanges()
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings == null) return;

        // Start from the original values every time, so recipes whose ammo is no longer
        // rebalanced (e.g. infinite mortar ammo turned off) go back to normal.
        RestoreOriginals();
        if (!settings.enableBalancing) return;

        foreach (var recipe in DefDatabase<RecipeDef>.AllDefs)
        {
            if (recipe.products == null || recipe.products.Count == 0) continue;

            var product = recipe.products[0];
            if (!AmmoBalanceManager.IsBalanced(product.thingDef)) continue;

            if (!originalData.TryGetValue(recipe, out var orig))
            {
                orig = new RecipeOriginalData
                {
                    productCount = product.count,
                    workAmount = recipe.workAmount,
                    label = recipe.label,
                    description = recipe.description
                };

                foreach (var ingredient in recipe.ingredients)
                {
                    orig.ingredientCounts[ingredient] = ingredient.GetBaseCount();
                }

                originalData[recipe] = orig;
            }

            product.count = 1;

            foreach (var ingredient in recipe.ingredients)
            {
                if (orig.ingredientCounts.TryGetValue(ingredient, out float origCount))
                {
                    ingredient.SetBaseCount(Mathf.Floor(origCount * settings.ammoCostMultiplier));
                }
            }

            recipe.workAmount = orig.workAmount * settings.ammoCostMultiplier;

            if (!string.IsNullOrEmpty(orig.label))
            {
                recipe.label = Regex.Replace(orig.label, @"x\d+", "x1");
            }

            if (!string.IsNullOrEmpty(orig.description))
            {
                recipe.description = Regex.Replace(orig.description, @"\d+ ", "1 ");
            }

            ResetLabelCache(recipe);
        }
    }

    private static void RestoreOriginals()
    {
        foreach (var kvp in originalData)
        {
            var recipe = kvp.Key;
            var orig = kvp.Value;

            if (recipe.products != null && recipe.products.Count > 0)
            {
                recipe.products[0].count = orig.productCount;
            }

            recipe.workAmount = orig.workAmount;
            recipe.label = orig.label;
            recipe.description = orig.description;

            ResetLabelCache(recipe);

            foreach (var ingredient in recipe.ingredients)
            {
                if (orig.ingredientCounts.TryGetValue(ingredient, out float origCount))
                {
                    ingredient.SetBaseCount(origCount);
                }
            }
        }
    }

    private static void ResetLabelCache(Def def)
    {
        Traverse.Create(def).Field("cachedLabelCap").SetValue(null);
    }
}
