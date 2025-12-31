using Verse;
using HarmonyLib;

namespace CombatExtendedInfiniteAmmo;

public class CombatExtendedInfiniteAmmoMod : Mod
{
    public static InfiniteAmmoSettings Settings { get; private set; }
    
    public CombatExtendedInfiniteAmmoMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<InfiniteAmmoSettings>();
        var harmony = new Harmony("com.stoneman.combatextended.infiniteammo");
        harmony.PatchAll();
    }
    
    public override string SettingsCategory()
    {
        return "CEInfiniteAmmo_ModName".Translate();
    }
    
    public override void DoSettingsWindowContents(UnityEngine.Rect inRect)
    {
        Settings.DoSettingsWindowContents(inRect);
    }
}
