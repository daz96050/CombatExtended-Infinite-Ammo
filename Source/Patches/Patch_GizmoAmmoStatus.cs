using HarmonyLib;
using Verse;
using UnityEngine;
using CombatExtended;
using RimWorld;
using System.Reflection;

namespace CombatExtendedInfiniteAmmo.Patches;
public static class Patch_GizmoAmmoStatus
{
    private static FieldInfo _bgTexField;
    private static Texture2D GetBGTex()
    {
        if (_bgTexField == null)
        {
            _bgTexField = typeof(GizmoAmmoStatus).GetField("BGTex", BindingFlags.NonPublic | BindingFlags.Static);
        }
        return (Texture2D)_bgTexField?.GetValue(null);
    }

    public static bool Prefix(GizmoAmmoStatus __instance, Vector2 topLeft, float maxWidth, GizmoRenderParms parms, ref GizmoResult __result)
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        var comp = __instance.compAmmo;
        bool isTurret = comp.turret != null;
        bool infinite = false;
        bool factionCheck = true;
        
        if (settings.playerFactionOnly)
        {
            if (isTurret)
            {
                if (comp.turret.Faction != Faction.OfPlayer) factionCheck = false;
            }
            else
            {
                if (comp.Wielder != null && comp.Wielder.Faction != Faction.OfPlayer) factionCheck = false;
            }
        }

        if (factionCheck)
        {
            if (isTurret && settings.infiniteTurretAmmo && comp.CurMagCount > 0) 
            {
                infinite = true;
            }
            else if (!isTurret && settings.infiniteAmmo) 
            {
                CombatExtended.AmmoDef currentAmmo = comp.CurrentAmmo;
                if (currentAmmo != null && AmmoInventoryHelper.HasSpecificAmmoInInventory(comp, currentAmmo))
                {
                    infinite = true;
                }
                else if (AmmoInventoryHelper.HasAnyAmmoInInventory(comp))
                {
                    infinite = true;
                }
            }
        }
        if (!infinite) return true;

        Texture2D bgTex = GetBGTex();
        float width = __instance.GetWidth(maxWidth);
        Rect backgroundRect = new Rect(topLeft.x, topLeft.y, width, Gizmo.Height);
        Rect inRect = backgroundRect.ContractedBy(6);
        GUI.DrawTexture(backgroundRect, bgTex);
        Text.Font = GameFont.Tiny;
        Rect textRect = inRect.TopHalf();
        string prefix = __instance.prefix ?? "";
        string ammoLabel = comp.CurrentAmmo == null ? comp.parent.def.LabelCap : comp.CurrentAmmo.ammoClass.LabelCap;
        Widgets.Label(textRect, prefix + ammoLabel);
        if (comp.HasMagazine)
        {
            // wow.. cyan blue with white text on the tiniest font..
            Rect barRect = inRect.BottomHalf();
            Widgets.FillableBar(barRect, 1f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Color oldColor = GUI.color;
            GUI.color = Color.black;
            Widgets.Label(barRect, "∞");
            GUI.color = oldColor;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        __result = new GizmoResult(GizmoState.Clear);
        return false;
    }
}

[StaticConstructorOnStartup]
public static class HarmonyPatcherDelayed
{
    static HarmonyPatcherDelayed()
    {
        var harmony = new Harmony("com.stoneman.combatextended.infiniteammo.delayed");
        var original = typeof(GizmoAmmoStatus).GetMethod("GizmoOnGUI", BindingFlags.Public | BindingFlags.Instance);
        var prefix = typeof(Patch_GizmoAmmoStatus).GetMethod("Prefix", BindingFlags.Public | BindingFlags.Static);
        
        if (original != null && prefix != null)
        {
            harmony.Patch(original, prefix: new HarmonyMethod(prefix));
        }
    }
}
