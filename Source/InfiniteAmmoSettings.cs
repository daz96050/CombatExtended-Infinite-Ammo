using Verse;
using UnityEngine;
using CombatExtendedInfiniteAmmo.Patches;

namespace CombatExtendedInfiniteAmmo;

public class InfiniteAmmoSettings : ModSettings
{
    public bool infiniteAmmo = false;
    public bool infiniteReserve = true;
    public bool infiniteTurretAmmo = false;
    public bool infiniteGrenades = false;
    public bool playerFactionOnly = true;
    public bool enableBalancing = false;
    public float ammoCostMultiplier = 5f;
    public bool limitAmmoStackSize = true;
    public bool limitAmmoSpawns = true;
    
    private bool _prevEnableBalancing = false;
    private float _prevAmmoCostMultiplier = 5f;
    private bool _prevLimitAmmoStackSize = true;
    
    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref infiniteAmmo, "infiniteAmmo", false);
        Scribe_Values.Look(ref infiniteReserve, "infiniteReserve", false);
        Scribe_Values.Look(ref infiniteTurretAmmo, "infiniteTurretAmmo", false);
        Scribe_Values.Look(ref infiniteGrenades, "infiniteGrenades", false);
        Scribe_Values.Look(ref playerFactionOnly, "playerFactionOnly", true);
        Scribe_Values.Look(ref enableBalancing, "enableBalancing", false);
        Scribe_Values.Look(ref ammoCostMultiplier, "ammoCostMultiplier", 5f);
        Scribe_Values.Look(ref limitAmmoStackSize, "limitAmmoStackSize", true);
        Scribe_Values.Look(ref limitAmmoSpawns, "limitAmmoSpawns", true);
        
        _prevEnableBalancing = enableBalancing;
        _prevAmmoCostMultiplier = ammoCostMultiplier;
        _prevLimitAmmoStackSize = limitAmmoStackSize;
    }

    
    public void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard list = new Listing_Standard();
        list.Begin(inRect);
        
        // Header
        Text.Font = GameFont.Medium;
        list.Label("CEInfiniteAmmo_Settings_Header".Translate());
        Text.Font = GameFont.Small;
        list.Gap();
        
        // Player Faction Only
        list.CheckboxLabeled(
            "CEInfiniteAmmo_PlayerOnly_Title".Translate(), 
            ref playerFactionOnly, 
            "CEInfiniteAmmo_PlayerOnly_Desc".Translate()
        );
        list.GapLine();
        
        // Infinite Ammo (No Reload)
        list.CheckboxLabeled(
            "CEInfiniteAmmo_InfiniteAmmo_Title".Translate(), 
            ref infiniteAmmo, 
            "CEInfiniteAmmo_InfiniteAmmo_Desc".Translate()
        );
        
        // Infinite Reserve
        list.CheckboxLabeled(
            "CEInfiniteAmmo_InfiniteReserve_Title".Translate(), 
            ref infiniteReserve, 
            "CEInfiniteAmmo_InfiniteReserve_Desc".Translate()
        );
        
        // Infinite Turret Ammo
        list.CheckboxLabeled(
            "CEInfiniteAmmo_InfiniteTurretAmmo_Title".Translate(), 
            ref infiniteTurretAmmo, 
            "CEInfiniteAmmo_InfiniteTurretAmmo_Desc".Translate()
        );
        
        // Infinite Grenades
        list.CheckboxLabeled(
            "CEInfiniteAmmo_InfiniteGrenades_Title".Translate(), 
            ref infiniteGrenades, 
            "CEInfiniteAmmo_InfiniteGrenades_Desc".Translate()
        );
        
        list.GapLine();

        // Balancing
        list.CheckboxLabeled(
            "CEInfiniteAmmo_BalanceMode_Title".Translate(), 
            ref enableBalancing, 
            "CEInfiniteAmmo_BalanceMode_Desc".Translate()
        );
        
        if (enableBalancing)
        {
            // Sub-options for balancing
            list.CheckboxLabeled(
                "CEInfiniteAmmo_LimitStackSize_Title".Translate(), 
                ref limitAmmoStackSize, 
                "CEInfiniteAmmo_LimitStackSize_Desc".Translate()
            );
            
            list.CheckboxLabeled(
                "CEInfiniteAmmo_LimitSpawns_Title".Translate(), 
                ref limitAmmoSpawns, 
                "CEInfiniteAmmo_LimitSpawns_Desc".Translate()
            );
            
            list.Gap();
            list.Label(
                "CEInfiniteAmmo_BalanceCost_Title".Translate() + ": " + ammoCostMultiplier.ToString("0.0") + "x"
            );
            ammoCostMultiplier = list.Slider(ammoCostMultiplier, 1f, 20f);
        }
        
        list.End();
        
        bool settingsChanged = enableBalancing != _prevEnableBalancing || 
                               ammoCostMultiplier != _prevAmmoCostMultiplier ||
                               limitAmmoStackSize != _prevLimitAmmoStackSize;
        
        if (settingsChanged)
        {
            _prevEnableBalancing = enableBalancing;
            _prevAmmoCostMultiplier = ammoCostMultiplier;
            _prevLimitAmmoStackSize = limitAmmoStackSize;
            AmmoRecipeManager.ApplyRecipeChanges();
            AmmoBalanceManager.ApplyBalanceChanges();
        }
    }
}
