# Combat Extended Infinite Ammo - Copilot Instructions

## Mod Overview

This is a RimWorld mod that patches CombatExtended's ammo system using Harmony. It provides different modes for managing ammo consumption.

## Ammo Modes

### Infinite Ammo
- The weapon magazine **never depletes**. Shots do not reduce `CurMagCount`.
- Pawns never need to reload at all.
- No ammo is consumed from inventory, ever.
- The loadout system should **not** count magazine ammo as inventory ammo (otherwise pawns won't pick up ammo they don't actually need).

### Infinite Reserve
- The weapon magazine **does deplete** normally as shots are fired.
- When the magazine is empty, the pawn performs a **reload animation** (normal reload flow).
- However, the reload **does not consume ammo from inventory**. The magazine is refilled without removing items.
- Unloading the weapon also **does not return ammo to inventory** (since nothing was consumed).
- Pawns should still keep real ammo rounds in their inventory for the loadout system to function correctly.
- The loadout system should count magazine ammo normally (CE default behavior) since real ammo exists in inventory.

### Key Distinction
| Behavior | Infinite Ammo | Infinite Reserve |
|----------|--------------|-----------------|
| Magazine depletes on shot | No | Yes |
| Reload animation plays | No | Yes |
| Ammo consumed from inventory on reload | No | No |
| Ammo returned to inventory on unload | No | No |
| Loadout keeps real ammo in inventory | No (not needed) | Yes |
| Loadout counts magazine as inventory | Should NOT (patched out) | Yes (CE default) |

### Infinite Turret Ammo
- Applies to all turrets **except** mortars.
- The turret magazine **never depletes**. Shots do not reduce `CurMagCount`.
- If a reload is triggered, the magazine is instantly refilled (no reload animation/delay).
- Magazine size is forced to 1 (effectively always has a round ready).
- `HasAmmo` always returns true.

### Infinite Mortar Ammo
- Applies only to mortars (`turret.def.building.IsMortar`).
- The mortar magazine **never depletes** while firing the same ammo type.
- If a reload is triggered with the **same** ammo type selected, the magazine is instantly refilled.
- If a **different** ammo type is selected, the normal reload flow runs (consumes new ammo, returns old) to allow ammo switching.
- `HasAmmo` returns true only when ammo is currently loaded (`CurrentAmmo != null && CurMagCount > 0`).
- Magazine size is **not** overridden (keeps normal mortar mag size).

## Architecture Notes

- `Patch_CompAmmoUser.cs` - Core patches for ammo consumption, reloading, and magazine behavior
- `Patch_LoadoutAmmoCount.cs` - Patches CE's internal `Utility_HoldTracker.GetStorageByThingDef` to exclude magazine ammo from loadout counting (Infinite Ammo only)
- `Patch_GizmoAmmoStatus.cs` - UI patch showing infinity symbol on ammo gizmo
- `Patch_Grenades.cs` - Prevents grenade/one-use weapon self-consumption
- `Patch_AmmoBalance.cs` - Optional balancing (stack limits, spawn limits, trader stock)
- `Patch_AmmoRecipes.cs` - Optional recipe cost adjustments

## Technical Notes

- `Utility_HoldTracker` is `internal` in CombatExtended, so patches must use `AccessTools.TypeByName()` for targeting.
- CE's `GetStorageByThingDef()` counts ammo in weapon magazines toward loadout satisfaction. This is correct for Infinite Reserve (real ammo in inventory) but wrong for Infinite Ammo (no real ammo needed).
- `CompAmmoUser.CurMagCount` = rounds currently in magazine
- `CompAmmoUser.CurrentAmmo` = the AmmoDef currently loaded
- `Integer` is a CE wrapper class with a public `value` field (mutable reference type for dictionary use)
- CE source code can be found here: ~/RiderProjects/CombatExtended

