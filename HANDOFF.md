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
side. Separately, testing also turned up Frontline's old Death-screen pixels lingering
under Home's tiles for several seconds after a RESTART. **Originally misdiagnosed as a
`screencap`/compositor testing artifact — it wasn't.** See "Hub back button + exit
sequencing" below for the real cause (Home's scene never had a camera, so nothing cleared
the screen once the last minigame camera unloaded) and the actual fix.

**Process note for future graduations:** mid-investigation here, I let the shared
Android-device lock go stale for 40+ minutes while deep in a debugging session instead of
re-acquiring it before each disruptive `adb` action — another concurrent session
(CrowdBattler/"We Are Warriors") correctly treated it as abandoned per the protocol and
took the device, which then confused my own testing for a while. Lesson relearned: take
the lock right before the actual `adb` action, release immediately after, even mid-debug —
don't hold one lock across a long multi-step investigation.

## FlowSort graduation (2026-07-27) — second minigame merge

Done. `Assets/Games/FlowSort/` holds FlowSort's Gameplay/UI scripts, Kenney art
(`Art/Kenney/`), and a ported `SceneBuilder.cs`/`ArtImporter.cs` (source project:
`D:\FlowSort`, formerly `D:\Pixel Flow` — the FlowSort session moved off that path
mid-project). `FlowSortMiniGame.cs` (originally handed off outside `Assets/` since
`Miniverse.Hub` doesn't exist in FlowSort's standalone project) now lives under
`Assets/Games/FlowSort/Scripts/` and is attached to the `GameManager` GameObject
directly in `SceneBuilder.Build()`. `displayName`/gameId is "FlowSort" — still a working
name per FlowSort's own HANDOFF, trivial to rename later via `FlowSortCatalogEntry.cs`
without touching the `gameId` save-data key. Verified end-to-end on the physical device:
Home → FlowSort tile → grid renders and is playable → exit button → back to a working,
re-launchable Home.

**Ported, not just copied**, following the lesson from Frontline's graduation: FlowSort's
own `SceneBuilder.cs`/`ArtImporter.cs` had the same class of hardcoded-path bug
(`Assets/Scenes/Main.unity`, `Assets/Art/Kenney/`) and the same `EditorBuildSettings.scenes`
clobber. Fixed on the way in rather than discovered after the fact this time. Also renamed
the scene file `Main.unity` → `FlowSortMain.unity` — Frontline's graduated scene already
claims `Main.unity`, and `SceneManager.LoadScene(name)` resolves scenes by filename across
the whole build, so two scenes sharing the name would be ambiguous (FlowSort's own HANDOFF
had already flagged this as a risk). Did not copy FlowSort's standalone `BuildScript.cs`/
`ProjectSetup.cs` — same reasoning as deleting Frontline's `BuildScript.cs`, they'd clobber
PocketVerse's PlayerSettings if ever invoked.

**Bugs found and fixed during this graduation:**

1. **Real bug, not a hunch:** `SceneBuilder.cs` wired the exit button with
   `exitButton.onClick.AddListener(gm.RequestExit)` *inside the editor-time `Build()`
   method*. A `Button.onClick` listener added via `AddListener()` is a C# delegate, and
   delegates don't survive scene serialization — only persistent calls added through the
   Editor's own Inspector do. The listener silently existed only in the transient in-memory
   scene while `Build()` ran, and vanished the moment the scene was saved to disk, so the
   button was permanently dead in any real build. Confirmed via `analytics.jsonl` showing
   `game_launch` with no matching `game_end`, then proved conclusively with a temporary
   file-based trace (logcat on this device churns too fast to trust for this) showing the
   click chain never even reached `Button.onClick`. Frontline's own `UIWire.cs` already
   solved this correctly (runtime-wires every button from a MonoBehaviour's `Start()`) and
   FlowSort's own `PowerupBar.cs`/`PetShop.cs` already followed that pattern too — the exit
   button was the one outlier wired the wrong way. Fixed the same way: `RevealGameManager`
   now has a public `ExitButton` field, assigned by `SceneBuilder`, wired in
   `RevealGameManager.Start()`.
2. Same latent issue as Frontline's Canvas-overlap bug, but for input: Home's `EventSystem`
   GameObject is a sibling of Home's Canvas, not a child of it, so hiding Home's Canvas via
   `_homeUIRoot.SetActive(false)` never touched it — it stayed enabled the whole time a
   minigame ran, alongside the minigame's own additively-loaded `EventSystem`. Two
   simultaneously-enabled `EventSystem`s is never correct regardless of whether it's the
   proximate cause of a given symptom (it wasn't, here — bug #1 above was, confirmed by the
   fact this fix alone didn't resolve the exit button). Fixed anyway: `HubLauncher` now also
   has a `_homeEventSystem` field, disabled/re-enabled in lockstep with `_homeUIRoot`.
   Regression-tested against Frontline afterward (PLAY still works, 62fps, no bleed-through)
   since this touches shared hub code every graduated game depends on.

Immediately after the exit button correctly unloads FlowSort (`analytics.jsonl` confirms
`game_end` fires right on schedule), `adb shell screencap` reliably showed FlowSort's last
frame and Home's tiles overlapping for several seconds. **Originally misdiagnosed as a
`screencap`/compositor artifact specific to this Huawei/EMUI device — it wasn't.** See
"Hub back button + exit sequencing" below for the real cause (Home's scene never had a
camera, so nothing cleared the buffer once the last camera in the loaded scene set was
destroyed) and the fix (`HomeBackgroundCamera`), confirmed via a direct on-device report
from Simon that the overlap was real, not a screenshot tooling quirk.

## Hub back button + exit sequencing (2026-07-28)

Frontline had no visible way back to PocketVerse — only an undocumented hardware/gesture
Back-button handler on its own Main Menu. Added a plain "X" button to Frontline's
persistent TopBar shell (`CanvasBuilder.BuildShell`, shown on Menu/Shop/Upgrades/Ranks),
reusing FlowSort's exact glyph/convention so both graduated games read as one consistent
"X always means back to PocketVerse" UI language rather than two different ideas for the
same action. Wired through a new `GameManager.ExitToHubOverride` static (mirrors the
existing `RestartOverride` pattern); the old Back-key handler and the new button both
funnel through the same `FrontlineMiniGame.RequestExit()` now instead of duplicating the
report-score-and-exit logic. TopBar's gear/lives/Supply pill got shrunk and re-spaced to
make room — verified on-device, no clipping or overlap in the new layout.

Also hardened `HubLauncher.UnloadActiveGameScene()`: Home used to reactivate its
Canvas/EventSystem the instant `UnloadSceneAsync` was *requested*, not once it actually
finished — a real (if narrow) window where both the outgoing minigame and Home could be
active simultaneously. Home now only reactivates in the unload's `.completed` callback.

**Correction, same day:** the "screencap artifact" read above (and the two earlier writeups
it references, in the Frontline and FlowSort graduation sections) was wrong. Simon
confirmed on the physical device, with his own eyes, that the overlap is real: pressing
the new X button left the outgoing game's last frame fully visible with Home's tile boxes
drawn on top of it. Real root cause: **Home's own scene has never had a camera at all** —
its Canvas is ScreenSpaceOverlay, which doesn't need one to draw, so nothing was ever
clearing the screen except whichever minigame's own camera happened to be loaded at the
time. The instant that minigame's scene unloads and takes its camera with it, there are
zero cameras left anywhere in the loaded scene set, so the GPU just keeps showing that
camera's last rendered frame indefinitely -- Home's tiles/title draw fine on top (Canvas
UI doesn't need a clear step), but nothing ever draws over the stale background behind
them. Invisible on the very first launch only because Home's intended background happens
to already be dark, so "undefined/uncleared" and "cleared to Home's own color" looked the
same by coincidence.

Fixed properly: `HubSceneBuilder.Build()` now adds a `HomeBackgroundCamera` to Home's own
scene -- `SolidColor` clear flag, `cullingMask = 0` (nothing to render, it only exists to
clear), `depth = -100` so any minigame's own camera (default depth 0) always draws over it
while one's loaded. Deliberately **not** tagged `MainCamera`: FlowSort's `TapInputRouter`
(and any future minigame) resolves taps via `Camera.main`, and a second camera wearing
that tag the moment a minigame is loaded alongside Home would make that lookup ambiguous.
Verified on-device: both Frontline's and FlowSort's exits now land on a clean Home
immediately (screenshot taken 1s after the tap, previously still showing the overlap after
5+ seconds), and a follow-up Frontline PLAY session confirmed no rendering regression
(clean 60fps, no interference from the new camera). The `UnloadActiveGameScene` sequencing
change above is still worth keeping (real, if narrow, correctness improvement) but was
never what fixed this -- the missing camera was the whole bug.

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

- **FlowSort** (`D:\FlowSort`, session "PocketVerse sort/flow puzzle minigame") — a
  picture-reveal shooter. Graduated 2026-07-27, see the section above. Public display name
  still pending a naming+collision-check pass (same process as PocketVerse's own); trivial
  to update later via `FlowSortCatalogEntry.cs` without touching the `gameId` save key.
- **CrowdBattler / "We Are Warriors"** (`D:\CrowdBattler`) — a crowd/stickman battler,
  applicationId `com.simonsvabenicky.mobmarch`. Not graduated yet as of 2026-07-27.

## Shared asset library + Home's real UI (2026-07-29)

**Asset library**: `D:\GameAssets\` (pre-existing, see its own `README.md`) got a second,
much larger batch of free UI/icon/VFX/audio packs sorted in, aimed specifically at giving
the hub a "premium casual mobile" look (thick borders, layered shadows, glossy buttons,
per Simon's reference screenshots). New top-level categories: `UI/`, `Icons/`, `VFX/`,
plus `Audio/SFX` and `Audio/UI-SFX`. Six packs in the batch were exact re-downloads of
packs already catalogued (deduped, originals kept); two long-standing "pending" gaps
(Modular Sci-Fi MegaKit, Fantasy Ambient pack) finally got filled in. Full pack-by-pack
license/source table is in the library's own README, not repeated here.

**Home's UI**, per Simon's "give the skeleton app real UI... have pocketverse feel like an
app" ask: the flat black/white Canvas (a Title, a bare `GridLayoutGroup`, an empty label,
nothing else) is gone. New pieces:

- `Assets/_Hub/Art/UI/` — a curated Kenney UI Pack subset (Blue/Grey/Yellow/Green gloss +
  gradient buttons, panels, a few icons), imported via `HubUIImporter.cs` (mirrors
  Frontline's own `UIImporter.cs` border-slicing logic exactly).
- `HubCanvasBuilder.cs` (`Assets/Editor`) — builds Home's whole UI in code, same
  "generated, not hand-authored" contract as Frontline's `CanvasBuilder.cs`, which this
  deliberately mirrors helper-for-helper. Background, a persistent top bar
  (profile/settings/sound/lives/cash) + bottom tab bar (Home/Store), and Settings/Store/
  Profile overlay panels.
- `ProceduralHomeBackground.cs` — a baked gradient + soft glow + scattered dot doodles,
  same technique as Frontline's `ProceduralMenuBackground`.
- `ProceduralAvatarIcon.cs` / hub-local copy of `ProceduralHeartIcon.cs` — no profile or
  heart sprite exists in the imported Kenney subset, so these bake simple silhouettes at
  Awake, same reasoning as Frontline's original heart icon.
- `HomeScreenController.cs` (rewritten) — game tiles are now real cards: a 9-sliced panel
  with a baked-in gradient/shadow (`button_rectangle_depth_gradient`, not the flat
  `input_rectangle`), a second darker copy offset behind for an actual drop shadow, a
  coloured round badge with the game's initial (no `MiniGameDef.icon` is set on any
  graduated game yet, so this is an honest placeholder, not a fake icon — swaps to the
  real one automatically the moment a game ships one), and the name below.
- `HubEconomy.cs` / `HubAudio.cs` / `HubStats.cs` — real, `PlayerPrefs`-persisted state
  behind the lives/cash counters, the sound toggle, and Profile's "games played" stat.
  Nothing spends/grants cash or lives yet (no game is wired to a shared economy), so it
  starts at a fixed opening balance rather than pretending a real economy exists — same
  honesty as the Store panel, which is a "more coming soon" placeholder, not a fake shop.
- `HomeShellController.cs` — wires all of the above at runtime (Button clicks are C#
  delegates, don't survive `EditorSceneManager.SaveScene`, same reasoning as everywhere
  else in this codebase).

**Bug found and fixed during this pass**: `GameObject.Find` cannot locate an *inactive*
GameObject, even mid-path (`"Canvas/SettingsPanel"` fails to resolve if `SettingsPanel`
itself is inactive, regardless of `Canvas` being active). First pass baked Settings/Store/
Profile inactive directly in `HubCanvasBuilder`, which meant `HomeShellController.Awake()`
silently failed to find any of them — tapping the settings gear just hid the game grid and
showed nothing. Fixed by leaving all three active in the saved scene (matching how
Frontline's own `CanvasBuilder` leaves every screen active and only `GameUI`'s
`RefreshCanvasVisibility` — here, `HomeShellController`'s `ShowHome()` call at the end of
`Awake()` — hides the non-current ones, *after* they've already been found). Also fixed:
two icons (`ProceduralAvatarIcon`'s fill colour, the cash pill's coin sprite) were near-white
against light backgrounds and read as invisible on-device; and Home's title overlapped the
new top bar (anchor math didn't account for the top bar's actual height fraction). All
confirmed fixed via on-device screenshots after each round.

**Found, NOT fixed here — pre-existing, not caused by this pass**: Frontline's own Shell
top-bar buttons (the "X" library button and the settings gear specifically) don't respond
to taps at all, in every screen state tested (main menu, mid-gameplay, the death screen).
Isolated carefully before concluding this: `PLAY`, `RESTART`, and `MAIN MENU` (different
canvases, same project, same build) all responded normally to taps computed the same way,
and the hardware Back key didn't trigger `RequestExit()` either. To rule out a hub-side
regression, FlowSort's exit button (a completely different mechanism —
`RevealGameManager.ExitButton`, not `GameManager.ExitToHubOverride`) was tested fresh in
the same build and returned to a clean Home instantly, proving `HubLauncher`'s
unload/reload path (and the `HomeBackgroundCamera` fix) both still work correctly. That
narrows this specifically to Frontline's own Shell buttons / `GameManager.ExitToHubOverride`
wiring — nothing in `Assets/Games/Frontline/` was touched this session. Not investigated
further since it's out of scope for the hub work this pass was about; worth a dedicated
look next time Frontline's own session picks up (same pattern as the Paused/Death-screen
bug already flagged above with its own worktree).
