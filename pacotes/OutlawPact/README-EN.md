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


# Outlaw Pact Beta 0.4.1 installation

Requires RDR2 PC Story Mode, ScriptHookRDR2 and an ASI loader. With the game closed, install OutlawPact.asi, OutlawPact.ini and the OutlawPact folder beside RDR2.exe.
Preserve your custom INI/settings/profile files when updating; merge the new sections into the old INI. Restart after editing INI controls or behaviour. Language changes from the panel apply immediately.

Default F7 opens the menu and G recruits or opens focused member commands. All controls are in [Controls]. F8/H/J is an optional example for setups already using F7/G.
Arrow/numpad navigation can be remapped or the numpad aliases disabled. NONE disables the recruitment/command hotkey. Menu key cannot be disabled. Invalid keys fall back to defaults; action collisions with menu/navigation disable that action; duplicate navigation reverts to defaults. Modifier/mouse keys are not supported.

World snapshots use OutlawPact/profiles/<Profile>.world and a .bak backup. They are independent of Rockstar saves and save slots. Set a unique Profile per playthrough. Loading an older game save restores the latest snapshot for that profile. Save explicitly through Settings before quitting; abrupt termination can lose changes since the last snapshot.
Saved camps load only near their saved site. Followers restore near the player; distant wait/guard anchors fall back near the player. Camp orders wait for the camp to load. Ongoing robberies, horses, personality, voice, exact facial morphs and custom weapon attachments are not fully serialized. Older sessions without snapshots cannot be recovered.

SaveGang/SaveCamp toggle restoring and updating the respective part of a profile; disabling them preserves the already saved data. A lowered MaximumCapacity or excluded member model may prevent restoration until corrected.

Translations cover all exposed labels/messages in 13 locales but remain drafts. Glyph rendering and gameplay require in-game testing. Other companion/gang mod compatibility has not been certified. Avoid controlling the same NPC with multiple mods.

Read NEXUS-DESCRIPTION-EN.md for features and current limits. This package is a beta candidate that passed mocked tests; it still needs RDR2 testing before publication.


## New in Beta 0.4.1

**Image-based panel.** A detailed leather/map background, 24 smooth transparent PNG icons and the licensed Rye western title font replace the pixel-style panel. Labels remain live, translated text; controls and individual companion commands are unchanged. The image panel requires windowed or borderless mode. Set [Interface] UseImagePanel=0 for the native fallback. Missing assets also trigger the fallback. The panel is a click-through Windows overlay; game-renderer-only screenshots may omit it.

**Camps on moderate slopes.** Each shelter, tent and decoration measures its own local ground and adjusts its position and tilt. Nearby placement searches help arrange tents on continuous, moderately curved terrain. The default slope limit is 32 degrees; steep cliffs, water and foundations with excessive gaps remain rejected. Optional furniture or bottles no longer prevent the entire camp from being built. Failed rebuilding preserves the previous camp. These are rigid props fitted to the ground, not deformable meshes.

New [Camp] settings: MaxSlopeDegrees, MaxUnevenness, MaxFoundationGap and SiteSearchRadius. Keep clear space for walking: these checks do not detect every obstacle or pathfinding issue.

