# Miniverse — hub app handoff

Written 2026-07-26. "Miniverse" is this project's internal/working name only — the
codebase, folder, git repo, and C# `Miniverse.Hub` namespace all keep using it, it's
harmless as scaffolding. **The public-facing app is branded "PocketVerse"**, decided
2026-07-26 after "Miniverse" turned out to collide with a registered trademark
(MGA'S MINIVERSE, Reg #8290433 — MGA Entertainment, who already ship their own "Miniverse"
mobile game) *and* an existing unrelated Play Store app with nearly the same name and
pitch ("MiniVerse - Mini Games Arcade"). Don't rename the project/code to match — only
`PlayerSettings.productName`/`companyName`, the Android applicationId, and in-app UI text
(the Home scene title) reflect the public name.

This is a minigame-hub app: one Play Store listing, many minigames inside it, each built
as its own ad-style playable (lane shooter, galaxy defense, highway overtake, color clash,
etc.) but never sold separately — the pitch is "try 20 games, we keep the ones people
actually play."

## Stack

Unity 6 (6000.5.5f1, same install Frontline uses), URP, new Input System, Android as the
primary/active build target. Android applicationId is `com.simonsvabenicky.pocketverse` —
deliberately namespaced under the dev's own identity rather than a generic
`com.<name>.app` pattern, because that generic pattern is exactly what collided with an
existing app the first time around (`com.miniverse.app` was already taken by an unrelated
published app).

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
    Frontline/        - graduated 2026-07-27, see "Frontline graduation" section below
    <Name>/           - one folder per graduated minigame
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

Ads and a shared player-progress/currency system are explicitly out of scope for now (Simon's
call, 2026-07-26) — revisit later, not a priority yet. Account/profile screens are similarly
untouched — building them now would mean guessing requirements before any real minigame has
graduated.

## Analytics

Built 2026-07-26, ahead of the general ads/currency deferral, because "which games are
actually played" is something worth tracking from the very first graduated game rather
than reconstructing after the fact. `Assets/_Hub/Scripts/Core/Analytics/`:

- `IAnalyticsBackend` — one sink interface (`LogEvent(name, params)`).
- `LocalFileAnalyticsBackend` — the only implementation right now. Appends one JSON line
  per event to `Application.persistentDataPath/analytics.jsonl` (on a real device:
  `/sdcard/Android/data/<applicationId>/files/analytics.jsonl`, pullable with `adb pull`).
  No account, SDK, or network dependency — deliberately, since this needs to work before
  any decision about a real backend gets made.
- `AnalyticsService` — the one static call site (`LogGameLaunch`/`LogGameEnd`).
  `HubLauncher` calls it automatically: `LogGameLaunch` when a minigame's scene finishes
  loading and `Init`/`StartGame` succeed, `LogGameEnd` (with duration + score) whenever
  that scene actually unloads — that covers both a real game-over (`OnMiniGameOver`) and a
  mid-game exit (`ReturnToHub` called directly), so abandoned sessions still count.

To answer "which games are played the most": count `game_launch` events per `gameId`. For
engagement: average `game_end.durationSeconds` per `gameId`. No dashboard yet — that's
manual analysis of the pulled file for now. A real dashboard (most likely Unity
Analytics/UGS, since it's already the same ecosystem as everything else here) is a second
`IAnalyticsBackend` added to `AnalyticsService`'s list later; no call site anywhere else
changes when that happens. The one step I can't do headlessly: linking a Unity Cloud
project via the Unity Dashboard requires an interactive login, so that's a manual step for
whenever a real dashboard is wanted.

Verified: `AnalyticsSmokeTest.cs` (`Assets/Editor`, `Miniverse/Analytics Smoke Test` menu
item) fires a fake launch/end pair since no real minigame exists yet to exercise
`HubLauncher`'s hooks naturally — confirmed it writes valid JSON lines, and confirmed the
whole thing survives a real Android/IL2CPP build (not just the Editor, which doesn't strip
code the way a device build does).

## Frontline graduation (2026-07-27) — first real minigame merge

Done. `Assets/Games/Frontline/` holds Frontline's Scripts/Art/Materials/Scenes/Audio plus
`FrontlineVolume.asset`. `FrontlineMiniGame.cs` adapts `GameManager`/`GameUI` to `IMiniGame`;
`GameManager.RestartOverride` redirects RESTART through `HubLauncher.ReturnToHub()` instead
of a `LoadSceneMode.Single` reload that would've also unloaded Home. Verified end-to-end on
the physical device: Home → Frontline tile → Frontline's own Menu → PLAY → gameplay → death
→ RESTART → back to a working Home.

**Bugs found and fixed during graduation, all now fixed in this project's copy** (the
Frontline session's own copy of these Editor scripts under `D:\Frontline` may still have
the originals — not urgent to sync back, but worth knowing if `D:\Frontline` ever gets
re-copied in without re-checking):

1. `FrontlineMiniGame` was written but never attached to any GameObject in the scene — a
   script existing on disk did nothing. Fixed by adding it to the `Systems` GameObject in
   `SceneBuilder.cs`.
2. Every copied Editor script (`SceneBuilder.cs`, `CanvasBuilder.cs`, `ArtImporter.cs`,
   `UIImporter.cs`) still hardcoded Frontline's *original* top-level paths
   (`Assets/Scenes/`, `Assets/Art/...`, `Assets/Materials/...`) instead of the graduated
   `Assets/Games/Frontline/...` location — including a `MakeMaterial()` helper inside
   `SceneBuilder.cs` with its own separate hardcoded path, easy to miss on a first pass.
   Left unfixed, rerunning any of these would either silently fail to find assets or write
   stray files at the project root (which happened once, cleaned up).
3. `SceneBuilder.Build()` used to overwrite `EditorBuildSettings.scenes` with *only*
   Frontline's scene — harmless when Frontline was the only scene in its own project,
   but inside the hub it would silently wipe Home (and any other graduated game) out of
   Build Settings if this script is ever run standalone without a following
   `BuildSceneSync.Sync()`. Removed; `BuildSceneSync` is the sole source of truth for
   Build Settings now.
4. `Editor/BuildScript.cs` was copied over as-is and would have silently overwritten
   PocketVerse's `companyName`/`productName`/`applicationIdentifier` back to Frontline's
   own branding if ever invoked (no MenuItem, so only reachable via an explicit
   `-executeMethod`, but still real risk sitting in the project). Deleted — `HubBuildScript.cs`
   already covers building the merged app.
5. `Assets/TextMesh Pro/` (Fonts/Resources/Shaders) was missing entirely — Frontline's UI
   uses TextMeshPro but the project never had TMP Essentials imported (Home's own UI
   deliberately uses legacy `UI.Text` to avoid that setup). Without it, every
   `TextMeshProUGUI.Awake()` threw a `NullReferenceException` on `TMP_Settings.defaultFontAsset`.
   Copied the folder over from Frontline with matching GUIDs.
6. Additive loading means Home's own Canvas stays in the hierarchy and rendering —
   without hiding it, "PocketVerse" and the empty tile box floated on top of (or
   interleaved with) live Frontline gameplay. `HubLauncher` now has a `_homeUIRoot`
   reference it disables on launch and re-enables on return.

**Known, not fixed here (out of scope):** Frontline has a real, reproducible bug where
pausing at the exact moment lives hit 0 leaves `GameUI` showing both the Paused and Death
screens simultaneously in a stuck state — confirmed via testing, not something I
introduced. There's already a dedicated Frontline-side session/worktree
(`D:\Frontline\.claude\worktrees\adoring-ptolemy-afe780`, branch
`claude/adoring-ptolemy-afe780`) working on exactly this; no action needed from the hub
side. Separately, testing also turned up what looks like a **stale-frame/compositor
artifact** on the physical test device (a Huawei running EMUI, visible in logcat as
`Hwaps`/`HwApsManager` overlay noise): after a clean RESTART, `adb shell screencap` would
sometimes keep showing Frontline's old Death-screen pixels for several seconds even though
diagnostic logging proved the scene had genuinely, quickly unloaded (`UnloadSceneAsync`
completing in ~30ms) and Home was functionally interactive underneath the whole time (a
second tap correctly launched a fresh Frontline instance). Current read: this is a testing
artifact specific to `adb`-based screenshotting on this device, not a real bug in
`HubLauncher`'s unload logic — but worth a sanity check by just watching the physical
screen directly next time, rather than only trusting `screencap`, before fully closing
this out.

**Process note for future graduations:** mid-investigation here, I let the shared
Android-device lock go stale for 40+ minutes while deep in a debugging session instead of
re-acquiring it before each disruptive `adb` action — another concurrent session
(CrowdBattler/"We Are Warriors") correctly treated it as abandoned per the protocol and
took the device, which then confused my own testing for a while. Lesson relearned: take
the lock right before the actual `adb` action, release immediately after, even mid-debug —
don't hold one lock across a long multi-step investigation.

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

Graduated 2026-07-27 — see the section above for what actually happened and what got
fixed. Frontline's own `D:\Frontline` project keeps developing independently exactly as
before; future updates get pulled into the hub by re-copying the relevant folders into
`Assets/Games/Frontline/` and re-checking the path constants in the copied Editor
scripts (easy to get wrong again — see the bug list above) rather than assuming a
straight copy just works.

## Other minigames in progress

- **FlowSort** (`D:\Pixel Flow`, session "PocketVerse sort/flow puzzle minigame") — a
  picture-reveal shooter, working name pending a naming+collision-check pass. Not
  graduated yet as of 2026-07-27.
- **CrowdBattler / "We Are Warriors"** (`D:\CrowdBattler`) — a crowd/stickman battler,
  applicationId `com.simonsvabenicky.mobmarch`. Not graduated yet as of 2026-07-27.
