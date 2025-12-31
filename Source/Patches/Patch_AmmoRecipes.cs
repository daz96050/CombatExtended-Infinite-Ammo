using Verse;
using RimWorld;
using CombatExtended;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CombatExtendedInfiniteAmmo.Patches;

public static class AmmoRecipeManager
{
    private static Dictionary<RecipeDef, RecipeOriginalData> originalData = new Dictionary<RecipeDef, RecipeOriginalData>();
    private static bool isPatched = false;
    private static float lastMultiplier = 0f;
    
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
        
        if (!settings.enableBalancing)
        {
            if (isPatched)
            {
                RestoreOriginals();
                isPatched = false;
            }
            return;
        }
        
        if (isPatched && lastMultiplier == settings.ammoCostMultiplier)
        {
            return;
        }
        
        if (isPatched)
        {
            RestoreOriginals();
        }
        
        int patchedCount = 0;
        
        foreach (var recipe in DefDatabase<RecipeDef>.AllDefs)
        {
            if (recipe.products != null && recipe.products.Count > 0)
            {
                var product = recipe.products[0];
                if (product.thingDef is AmmoDef)
                {
                    if (!originalData.ContainsKey(recipe))
                    {
                        var data = new RecipeOriginalData
                        {
                            productCount = product.count,
                            workAmount = recipe.workAmount,
                            label = recipe.label,
                            description = recipe.description
                        };
                        
                        foreach (var ingredient in recipe.ingredients)
                        {
                            data.ingredientCounts[ingredient] = ingredient.GetBaseCount();
                        }
                        
                        originalData[recipe] = data;
                    }
                    
                    product.count = 1;
                    
                    var orig = originalData[recipe];
                    
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
                    
                    patchedCount++;
                }
            }
        }
        
        isPatched = true;
        lastMultiplier = settings.ammoCostMultiplier;
    }
    
    private static void RestoreOriginals()
    {
        Log.Message($"[CE Infinite Ammo] Restoring {originalData.Count} recipes...");
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

[HarmonyPatch(typeof(AmmoInjector), nameof(AmmoInjector.Inject))]
public static class Patch_AmmoInjector_Inject
{
    public static void Postfix()
    {
        AmmoRecipeManager.ApplyRecipeChanges();
    }
}
