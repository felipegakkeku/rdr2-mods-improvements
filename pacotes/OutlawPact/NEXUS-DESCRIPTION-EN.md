## Beta 0.4.4 — lightweight native panel

- Native RDR2 panel is now the default (UseImagePanel=0).
- Default menu does not create Windows image layers, call UpdateLayeredWindow, decode PNGs or load external fonts.
- Replaced per-pixel native icons with numbered option indicators.
- Cached menu labels/wrapping and refreshed roster/status data at up to 4 Hz; navigation/drawing remain frame-driven.
- Native transitions change only position/colour, without image uploads.
- Menus, 13 languages, configurable controls, individual commands and manual saving retained.

The default panel has simpler visuals than the PNG overlay. The custom flag is unchanged and still requires windowed/borderless; the native menu does not. Legacy image panel is opt-in via UseImagePanel=1 and is not recommended for setups experiencing overlay stalls.

1,000 simulated selection changes created no layered windows and initialized no GDI+/PNG/font resources. Native draw was bounded to 32 rectangles and 30 labels across 13 locales. Compilation, mock ASI loading and gameplay/restoration regressions passed. Actual RDR2 performance/stall resolution remains to be verified.



Earlier release notes / Notas das versões anteriores:

## Beta 0.4.3 — panel performance, transitions and custom flag

- Cached resized panel background, icons and text; reused font families and startup warm-up.
- Bounded panel update cadence/render dimensions; unchanged frames are reused.
- Lightweight page/open transitions and Enter confirmation. [Interface] Animations=0 disables them.
- Menu navigation no longer triggers whole-gang snapshot writes. Automatic world saving follows its configured interval and waits until the menu closes; explicit Save remains immediate.
- Threat candidates are filtered once per AI cycle for all companions; nearby interaction scanning is reduced.
- Custom PNG camp flag with a pole and visual cloth deformation driven by native game wind direction/speed.
- Cached bounded flag texture, distance culling and asynchronous centre-point occlusion check.
- Image: OutlawPact/assets/flag.png. Tutorial included; existing custom flag images are preserved by the local updater.
- Retains 0.4.2 restoration fixes and 0.4.1 terrain fitting.

Windowed/borderless mode is required for the image layers. The flag is projected visual animation, not native cloth physics/collision; partial occlusion is approximate. Hidden during menus/missions and beyond draw distance. Game-renderer-only captures may omit image layers.

An isolated 520x1060 redraw benchmark improved from approximately 65 ms to 2 ms with warm caches; this is not RDR2 FPS or a zero-stutter guarantee. Actual RDR2 performance/rendering validation remains pending.


## Beta 0.4.2 — restoration stability

- Automatic restoration waits 10 seconds and checks player readiness.
- Recreated NPCs wait for visual readiness before clothing, weapons and orders.
- Structural head/body/eye components are retained from the initialized model. Only supported clothing is replayed, including from older snapshots. Exact civilian facial identity is not guaranteed.
- Weapon assets are requested and awaited before weapons are granted.
- Readiness failure/timeouts clean up partial actors and preserve the snapshot; failed manual restoration keeps the existing gang.
- Added per-stage restoration diagnostics.

The observed log ended after world_restored without an exact crash cause. Risky restoration paths were corrected and simulated regression tests passed; actual RDR2 crash resolution remains to be confirmed.


# Outlaw Pact — Build your own gang

Turn nearby civilians into your own outlaw crew, give each member individual orders, and build a camp that grows with your gang. Outlaw Pact brings gang management to Red Dead Redemption 2 Story Mode, with recruitment, matching clothing, combat support, weapon collection and paid upgrades.

## What you can do

- **Recruit civilians:** an invitation can be accepted, refused, or end with the civilian fleeing or reporting you. Recruitment chances are configurable.
- **Give individual orders:** tell a specific member to follow, wait, guard a position, return to camp or guard the camp. The member roster is available from the main panel.
- **Build your camp:** each active member receives a separate tent. The camp includes a shared shelter, campfire, table, chairs and beer bottles. Placement checks reject unsuitable terrain.
- **Dress as a gang:** compatible recruits receive the player's clothing components. Their model and face are not copied from the player. Some body types and clothing combinations cannot be shared.
- **Collect dropped weapons:** members following the player can pick up supported loose firearms. Unarmed members equip a collected weapon; spare guns enter the gang's stock for sale at camp.
- **Improve your crew:** spend money at camp to increase capacity and training or arm unarmed members. Members remain vulnerable; this is not an invincibility mod.
- **Rob civilians:** threaten an aimed civilian or order an armed member to attempt a robbery. Targets can resist, and crimes can be reported.
- **Build reputation:** successful robberies can increase reputation, affect recruitment chances and trigger recognition messages. Reputation settings can be adjusted or disabled.

## New in Beta 0.4.1

**Image-based panel.** A detailed leather/map background, 24 smooth transparent PNG icons and the licensed Rye western title font replace the pixel-style panel. Labels remain live, translated text; controls and individual companion commands are unchanged. The image panel requires windowed or borderless mode. Set [Interface] UseImagePanel=0 for the native fallback. Missing assets also trigger the fallback. The panel is a click-through Windows overlay; game-renderer-only screenshots may omit it.

**Camps on moderate slopes.** Each shelter, tent and decoration measures its own local ground and adjusts its position and tilt. Nearby placement searches help arrange tents on continuous, moderately curved terrain. The default slope limit is 32 degrees; steep cliffs, water and foundations with excessive gaps remain rejected. Optional furniture or bottles no longer prevent the entire camp from being built. Failed rebuilding preserves the previous camp. These are rigid props fitted to the ground, not deformable meshes.

New [Camp] settings: MaxSlopeDegrees, MaxUnevenness, MaxFoundationGap and SiteSearchRadius. Keep clear space for walking: these checks do not detect every obstacle or pathfinding issue.

## New in Beta 0.4

**Configurable controls.** Change `MenuKey`, `RecruitKey` and `CommandKey` independently in `OutlawPact.ini`, then restart the game. The on-screen hints use your configured keys. Default controls remain F7 and G. Recruitment and commands can share a key or use separate keys. Interaction hotkeys are ignored while the player is in a vehicle by default.

**Persistent gang and camp profiles.** The mod saves a separate world snapshot containing member models, component appearance, health, supported weapons and ammo, slots, stable orders and camp coordinates. Snapshots are restored after returning to Story Mode or restarting the game. Saved camps are rebuilt when you approach their saved location and the terrain is available. Follow orders resume near the player; distant wait/guard positions fall back near the player to avoid spawning actors on unloaded terrain. Active robberies are not resumed.

Automatic world saving runs every 10 seconds by default, waits while the menu is open and resumes after it closes. Manual Save remains immediate. Settings also includes **Save gang and camp** and **Restore saved gang and camp**. Snapshot writes are atomic and keep a previous valid backup. An invalid or failed restoration does not overwrite the saved gang with an empty roster.

Menu navigation keys are configurable as `UpKey`, `DownKey`, `ConfirmKey` and `BackKey`; the optional numpad navigation can be disabled.

**More INI settings.** Configure capacity, recruitment outcomes, follow distance and speed, automatic defence, combat accuracy/movement/range, weapon collection, camp activities and restore radius, reputation and persistence.

**Language selection and pictograms.** Every action has an icon. Choose a language through the settings gear or keep automatic detection from the game.

## Languages

All currently exposed menu labels and gameplay messages have locale entries for English, French, German, Italian, Spanish (Spain), Portuguese (Brazil), Polish, Russian, Korean, Traditional Chinese, Japanese, Spanish (Mexico), and Simplified Chinese. Russian and both Chinese variants are included.

Translations are drafts and welcome corrections from native speakers. In-game glyph rendering still needs testing across language installations. Language preference is saved in `OutlawPact/settings.ini`.

## Requirements and installation

Requires **Red Dead Redemption 2 for PC**, **Story Mode**, **ScriptHookRDR2** and an **ASI loader**. Lenny's Mod Loader, WERO and bank robbery mods are not required.

With the game closed, extract `OutlawPact.asi`, `OutlawPact.ini` and the `OutlawPact` folder beside `RDR2.exe`. When updating, preserve customized configuration, language settings and profile files. Check the included README for the new configuration sections. An optional compatibility example uses F8 / H / J; choose keys that are free in your own setup.

## Compatibility and current limits

Compatibility with **Ultimate Companion System**, **RedLua** and other companion/gang mods has **not been confirmed by in-game testing**. Separate keys address a hotkey collision; they do not prove AI compatibility. Avoid recruiting the same NPC with two mods.

Outlaw Pact refuses mission NPCs and NPCs already in a group. With `YieldToExternalGroups=1`, it drops control if a member enters an external group or their relationship group changes, without resetting that NPC's tasks or health. This cannot detect every other mod's ownership method. An `ExcludedModels` list and optional combat/loot switches help narrow its scope.

World snapshots belong to the mod's named `Profile`; they are **not embedded in Rockstar saves or automatically matched to individual save slots**. Use separate profile names for separate playthroughs. Loading an older game save restores the latest snapshot for that profile, not the historical gang state from that save. Appearance captures component assets; exact facial morphs, personality, horses, voices and custom weapon attachments are not fully serialized.

Use a clear camp site on continuous, moderately sloped ground. Individual props fit their local terrain; steep cliffs, water and excessive foundation gaps are rejected. Ground placement checks do not detect every obstacle or pathfinding issue. Maximum supported capacity is 12. Hostage negotiation and bank integration are excluded from this release. The custom flag uses visual projection with the limitations described above.

## Testing and feedback

This build passed Windows x64 compilation, ASI loading with a mock ScriptHook, and simulated tests for controls, localization, ownership yielding, recruitment, camp placement, individual orders, snapshot integrity and restoration. This is **not a substitute for testing the Beta 0.4.1 build inside RDR2**.

When reporting an issue, include your game build, ScriptHook version, other gang/companion mods, configured controls and the steps that triggered it. Russian and Chinese translation corrections are especially welcome.

Created by **LucaixDEV**.
