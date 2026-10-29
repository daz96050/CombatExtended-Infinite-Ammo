# Combat Extended - Infinite Ammo

![tenor](https://github.com/user-attachments/assets/ec3919dc-1495-4aba-bb1c-ed9b6243f9d3)

Infinite ammo mod for the Combat Extended mod, and some small attempts to at least balance this thing (just a little bit).
This mod plans to give all Combat Extended weapons infinite ammo in various modes.

**NOTE:** This mod requires Combat Extended!
Please look at the original [here](https://github.com/CombatExtended-Continued/CombatExtended)!

## Features
### Infinite Ammo - No Reloading
- Toggleable.
- Makes all guns infinite just like vanilla, every gun can shoot infinitely as long as you have one ammo type in your inventory.
### Infinite Reserve - With Reloading
- Toggleable.
- Makes all guns infinite, but you have a mag size. You will still be required to reload as long as you have one ammo type in your inventory. Any ammo loaded will not be consumed from your inventory at all.
- Weapons without a magazine (bows, etc.) shoot without using up their ammo.
### Require Ammo In Inventory
- On by default. Turn it off and pawns can load any ammo type their weapon accepts without carrying any: pick the type from the reload button.
### Infinite Turret Ammo
- Toggleable.
- Makes turrets have infinite ammo. You just need to load it with one round and it will be able to fire it forever.
### Ammo Balancing
- Only applies to ammo that something gets infinite ammo for: mortar shells are untouched unless Infinite Mortar Ammo is on, turret-only ammo unless Infinite Turret Ammo is on, and so on.
- Recipes now yield 1 ammo at a time with multiplier for work and resource costs.
- Changes ammo stack limits to 1.
- Limits traders, quest rewards and loot to a few single rounds of any ammo.
- Generated pawns (raiders, visitors, traders, new mechs) carry at most 1 round of each ammo type.
- All these are toggleable.

## Balance
With ammo balancing enabled, the mod now provides a much more balanced experience:
- Since you only need 1 bullet to have infinite ammo, you only get 1 bullet from various sources.
- Market values reflect the actual cost of crafting a single bullet, not inflated recipe costs.
- Traders won't flood you with hundreds of free infinite ammo.

Recommended setup: **Infinite Reserve** mode with all **Ammo Balancing** settings enabled. This gives you realistic reloading with infinite reserve ammo, while keeping the economy intact.

### Key Distinction
| Behavior | Infinite Ammo | Infinite Reserve |
|----------|--------------|-----------------|
| Magazine depletes on shot | No | Yes |
| Reload animation plays | No | Yes |
| Ammo consumed from inventory on reload | No | No |
| Ammo returned to inventory on unload | No | No |
| Loadout keeps real ammo in inventory | No (not needed) | Yes |
| Loadout counts magazine as inventory | No (patched out) | No (patched out: loading is free, so counting it makes pawns drop "excess" ammo) |

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

## Compatibility
Compatible with all CE weapon/ammo mods.
Probably incompatible with mods that changes stack sizes. If there are problems, try turning off certain settings.

## Building
You will be required to have [Combat Extended](https://github.com/CombatExtended-Continued/CombatExtended) in the same parent folder to compile this. Run dotnet compile in the source folder, it should just work.

## License
This mod is a mod of another mod, and is used under the [Creative Commons 4.0 License](https://creativecommons.org/licenses/by-nc-sa/4.0/).
