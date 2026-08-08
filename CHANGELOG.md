# Changelog

All notable changes to CoolerStims are documented here. Format follows [Keep a Changelog](https://keepachangelog.com/); versions follow [SemVer](https://semver.org/).

## [Unreleased]

## [2.0.1] - 2026-08-07
### Added
- APEX now raises the Vitality skill for its 110 second window, on top of everything it already did. Less chance of bleeding out when you get hit mid-push. Tunable in `config.json` like the rest of the buffs

### Changed
- Now requires **SPT 4.1.2 or newer**. It will not load on 4.1.1

### Fixed
- GRAFT can now go in the Injector case. The case only takes stimulants plus Morphine specifically, and GRAFT is a Morphine clone filed under Drugs (that is what lets its buffs and limb healing work at the same time), so it matched neither

## [2.0.0] - 2026-08-02
### Changed
- Now targets SPT 4.1 — requires **SPT 4.1.1 or newer** and will not load on 4.0.x. 4.1.0 is excluded on purpose: it shipped a client bug that stopped mod bundles loading, so the stims showed a placeholder model there. Fixed by SPT in 4.1.1.
- Server mod rebuilt for .NET 10 and the 4.1 mod API (interface-based metadata, async load, injected database tables)
- Server half now installs to `SPT_Runtime/user/mods/CoolerStims/` — the server path moved in 4.1
- Dropped the unused `package.json`, a leftover from the TypeScript-era server
### Fixed
- Profiles holding CoolerStims items being rejected at startup as "modded item not in items db". The stims are now registered before the server validates profiles.

## [1.4.0] - 2026-07-18
### Added
- Custom in-game models and icons for all five stims — each stim now has its own look (APEX red/orange with circuit accents, AEGIS white/green with heartbeat motif, IRON gunmetal with heat-tinted cap, ARGUS violet/yellow with radar plate, GRAFT clinical white/red with dose windows) instead of reusing the vanilla donor items' appearance. Ships as asset bundles; stash icons, inspect view, and in-world models all use the new skins
- New client plugin (`CoolerStimsClient.dll`): the custom skins now also show on the injector in your hands during the use animation. This additionally fixes the stims previously showing random vanilla stim skins (Zagustin, SJ1, ...) when used — the game picks the in-hands skin by item id and doesn't know custom ids, so the plugin overrides it. Toggleable in the F12 menu
### Fixed
- One-second delay before weapons came back up after using APEX, AEGIS, IRON, or ARGUS — use time now matches the 2-second injection animation (GRAFT already did)

## [1.3.0] - 2026-07-12
### Added
- ARGUS Perception Stim — sharpens attention, perception, and search speed for looting/salvage, with an energy boost and a tunnel-vision comedown
- GRAFT Trauma Injector — multi-dose auto-injector that restores destroyed/fractured limbs (like CMS/Surv12) while granting HealthRate, EnergyRate, and HandsTremor buffs; 5 charges, one destroyed limb per charge
- Both new stims added to loot tables and loose loot

## [1.2.0] - 2026-06-29
### Added
- config.json for user-customizable buff values, durations, trader prices, and loyalty levels
### Changed
- AEGIS healing rate increased from 2.5 to 9.5 HP/sec
- APEX trader price updated to 84,379 roubles; AEGIS to 105,836; IRON flea price to 179,975

## [1.1.1] - 2026-06-27
### Changed
- Rebalanced pricing based on vanilla stim comparisons (APEX 42,000 | AEGIS 50,000 | IRON 55,000)
- APEX and AEGIS moved to Therapist LL4

## [1.1.0] - 2026-06-27
### Added
- Quantum Tunnelling and Health skill buff on APEX
### Fixed
- Loot spawns (loose loot and static containers)
- Flea market purchasing
- Med container targeting (Medcase, Medbag SMU06, Medical supply crate)
### Changed
- Spawn weights balanced against actual game data
- Removed HP resource bar display for single-use stims

## [1.0.0] - 2026-06-26
### Added
- Initial release — APEX, AEGIS, and IRON combat stimulants
