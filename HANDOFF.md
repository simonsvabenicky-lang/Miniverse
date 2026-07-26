# Miniverse — hub app handoff

Written 2026-07-26. Miniverse is the minigame-hub app: one Play Store listing, many
minigames inside it, each built as its own ad-style playable (lane shooter, galaxy
defense, highway overtake, color clash, etc.) but never sold separately — the pitch is
"try 20 games, we keep the ones people actually play."

## Stack

Unity 6 (6000.5.5f1, same install Frontline uses), URP, new Input System, Android as the
primary/active build target. `com.miniverse.app` is the placeholder Android
applicationId / `Miniverse` the company name — both trivially renameable in
ProjectSettings before a real store listing, just needs deciding.

## Architecture: one Unity project, "graduate via merge"

Decided jointly with the Frontline session (see `D:\Frontline\HANDOFF.md`). Three options
were on the table — shared project from day one, dynamic AssetBundles/Addressables, or
graduate-via-merge — and graduate-via-merge won because it lets every minigame keep
developing as its own fully independent Unity project/session (no concurrent-editing risk
on shared scene/prefab YAML) while still shipping as a single simple APK.

**What "graduate" means in practice:** once a minigame is far enough along, copy its
Assets (scripts, already-imported art, prefabs, scenes) into
`Miniverse/Assets/Games/<Name>/`, and make its entry point implement `IMiniGame`
(`Assets/_Hub/Scripts/Core/IMiniGame.cs`) so the hub can start/pause/end it and read its
score without knowing anything else about it. The minigame's own save/currency system
either gets left alone (self-contained) or wired into a shared hub-level system later —
not decided yet, not needed until the first real graduation.

## Folder layout

```
Assets/
  _Hub/
    Scripts/Core/    - IMiniGame, MiniGameDef, GameCatalog, HubLauncher
    Scripts/UI/       - HomeScreenController
    Scenes/Home.unity - the only scene that's never unloaded
  Games/
    <Name>/           - one folder per graduated minigame (empty for now — Frontline
                        hasn't graduated yet, still being built in its own session)
  Shared/             - pulled from D:\GameAssets as needed, per-game ArtImporter style
  Settings/           - URP pipeline/renderer assets, copied from Frontline so mobile
                        rendering is already tuned the same way as a device-verified game
  Editor/             - HubSceneBuilder, BuildSceneSync, HubBuildScript
```

## Why scenes and Build Settings are code-generated, not hand-edited

Same rule as Frontline: `Assets/_Hub/Scenes/Home.unity` is a build artifact from
`HubSceneBuilder.Build()` (`Miniverse/Rebuild Home Scene` menu item), not something
hand-assembled in the Editor. Regenerate it by editing the script and rerunning, never by
dragging things around in the Scene view — otherwise it silently drifts from source and
nobody can tell why.

`BuildSceneSync.Sync()` (`Miniverse/Sync Game Scenes To Build Settings`) rebuilds Build
Settings' scene list from whatever `.unity` files actually exist under `Assets/Games/`,
Home always first. Run it after every graduation. This exists for the same
reason `MiniGameDef`/`GameCatalog` exist below: no shared file that two different
graduations would both need to hand-edit.

## The plug-in mechanism: MiniGameDef + GameCatalog

A graduated game adds one `MiniGameDef` ScriptableObject asset (gameId, displayName,
sceneName, icon, category) under
`Assets/Games/<Name>/Resources/GameCatalog/<Name>.asset`. `GameCatalog.All`
(`Assets/_Hub/Scripts/Core/GameCatalog.cs`) finds every one of these via
`Resources.LoadAll("GameCatalog")` — Unity merges same-relative-path `Resources` folders
project-wide, so this needs zero edits to any file another game's graduation also
touched. `HomeScreenController` reads `GameCatalog.All` at runtime and builds one tile per
entry (no prefab asset — tiles are built in code, same generate-don't-author reasoning as
the scene itself).

## Launch flow

`HubLauncher` (singleton on `HubBootstrap` in Home.unity, `DontDestroyOnLoad`) loads a
minigame's scene additively over Home, finds the component implementing `IMiniGame` in
that scene, calls `Init`/`StartGame`. The minigame calls
`MiniGameContext.ReportGameOver(score)` when it's done, which routes back through
`HubLauncher.OnMiniGameOver` → `SaveProgress()` → unloads the minigame's scene. Home never
unloads, so hub-level UI state survives a play session with no extra save/restore needed.

## What's deliberately NOT built yet

Ads, analytics, a shared player-progress/currency system, and account/profile screens are
all real work the fake-Claude brainstorm mentioned but this skeleton intentionally skips —
building them now would mean guessing requirements before any real minigame has
graduated. Revisit once Frontline (or whichever game graduates first) actually needs one
of these, so the design is driven by a real integration instead of speculation.

## Current state (2026-07-26)

- Unity project created, URP + Input System + package set mirrored from Frontline's
  `manifest.json`, mobile-tuned `Mobile_RPAsset`/`Mobile_Renderer`/`QualitySettings`
  copied from Frontline (GUIDs preserved) so rendering starts from a config already
  verified on a real device rather than from scratch.
- Android is the active build target. Portrait-only orientation, matching Frontline.
- Core scripts written: `IMiniGame`, `MiniGameDef`, `GameCatalog`, `HubLauncher`,
  `HomeScreenController`, plus `HubSceneBuilder`/`BuildSceneSync`/`HubBuildScript` in
  `Assets/Editor`.
- Home scene generated and building successfully in batchmode (empty-state placeholder
  shown, since `Assets/Games/` has nothing graduated into it yet).
- Verified end-to-end with a real headless Android build (`HubBuildScript.BuildAndroid`):
  `Builds/Android/Miniverse.apk` built clean, no compile or IL2CPP errors. Not yet
  installed/run on the physical test device.
- Git repo initialized locally (no remote yet), identity matches Frontline's local repo
  config. `.gitignore`/`.gitattributes` mirror Frontline's.

## For the Frontline session

Nothing changes about how Frontline develops day to day — keep going in its own project
exactly as now. The only future step is graduation: when Frontline is ready, copy its
Assets into `Assets/Games/Frontline/` here, add a `Frontline : MonoBehaviour, IMiniGame`
wrapper (or make `GameManager` implement it directly) around its existing
`StartGame`/pause/`GetScore`/save calls, drop a `MiniGameDef` asset in
`Resources/GameCatalog/`, and run `Miniverse/Sync Game Scenes To Build Settings`. Not
urgent — Frontline isn't there yet.
