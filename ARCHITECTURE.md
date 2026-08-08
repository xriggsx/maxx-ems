# EMS Roleplay Mod — Architecture & Build Plan

> **Status (2026-08-08):** This is the original architecture blueprint the project
> was built from. **Phases 0–3 are complete** (see `DEVELOPMENT.md` for current
> state and what's next). The design below is still the source of truth for *how*
> the systems fit together, but a few implementation details ended up differing
> from this plan — noted inline where relevant and summarized here:
>
> - **Config/log path resolution:** the plan said resolve via
>   `Assembly.GetExecutingAssembly().Location`. That was **wrong** — RPH loads each
>   plugin into its own AppDomain from a temp shadow-copy, so that resolves to a
>   temp folder. `ModPaths` now derives the path from the **game process exe
>   location** instead.
> - **Vendored SDK filename:** the reference DLL is `lib/RagePluginHookSDK.dll`
>   (assembly identity `RagePluginHook`), not `RagePluginHook.dll`.
> - **CalloutBase reentrancy:** implemented as a per-tick state-advance **loop**
>   rather than an `_isTransitioning` flag — same goal (no double transition from
>   one input read), simpler mechanism.
> - **Console commands:** must use `[ConsoleCommand(Name = "...")]` (named
>   property) and register via `Game.AddConsoleCommands(new[] { typeof(...) })`
>   (explicit Type[] overload). The positional-string ctor and parameterless
>   register both misbehaved. An `EntryPoint.Main` re-entry guard was also added
>   against intermittent double-invocation (unconfirmed root cause — see
>   `DEVELOPMENT.md`).
> - **Target framework:** .NET Framework **4.8** / x64 (the plan left this as an
>   open decision; the RPH SDK build settled it).

## Context
The goal was to start clean and organized, specifically avoiding the classic gaps
this kind of mod tends to accumulate: orphaned entities from incomplete cleanup,
features wired to only keyboard or only controller, per-callout duplicated logic
instead of a shared base, config that crashes on malformed input, and edge cases
(player enters a vehicle mid-interaction, player walks away, game paused mid-timer)
that get forgotten because they aren't built into a shared mechanism.

The approach below closes each of those gaps *structurally* — one shared mechanism
that every callout automatically inherits — rather than relying on each callout
author remembering a rule. Confirmed decisions: **single active callout at a time**
(simpler state machine/cleanup/watchdog reasoning, can relax later) and **fully
custom-drawn UI** (no third-party dependency, full control, goes through our own
unified input layer).

## 1. Solution / Folder Structure

```
maxx ems/
  EmsMod.sln
  lib/
    RagePluginHookSDK.dll           <- vendored reference-only copy, committed to git
  src/
    EmsMod/
      EmsMod.csproj
      Properties/AssemblyInfo.cs
      EntryPoint.cs                  <- [assembly: Rage.Attributes.Plugin(...)] + Main()
      Core/
        EntitySpawnRegistry.cs       <- central spawn/cleanup ledger
      Callouts/
        CalloutBase.cs
        CalloutState.cs              <- state machine enum
        CalloutManager.cs            <- dispatch selection, single-active-callout enforcement
        TestCallout.cs               <- content-free vertical slice
        CarAccident/
          CarAccident_Bleeding.cs    <- (Phase 4, not yet built)
        (one folder per CalloutType, thin subclasses only)
      Config/
        ConfigLoader.cs
        ConfigValidator.cs
        Schema/
          GeneralConfig.cs
          CalloutConfig.cs           <- (added in Phase 4)
          InjuryConfig.cs            <- (added in Phase 4)
          DialogueLineConfig.cs      <- (added in Phase 4)
      Input/
        InputManager.cs
        InputAction.cs
        InputBinding.cs
      Dialogue/
        DialogueEngine.cs
        IDispatchVoice.cs
        SystemSpeechVoice.cs        <- Phase 1 voice (TTS)
        NAudioVoice.cs               <- Phase 6 stub, added later
      UI/
        PromptUI.cs                  <- accept/decline popup
        AssessmentMenuUI.cs          <- (added in Phase 4)
      Debug/
        DebugCommands.cs             <- temporary smoke-test console commands
      Utils/
        Log.cs
        ModPaths.cs
        Safe.cs
  dist/                              <- git-tracked build output staging
    Plugins/
      EmsMod.dll / EmsMod.pdb        <- produced by post-build copy step
      EmsMod/
        Config/
          General.xml
          Callouts/
            CarAccident_Bleeding.xml <- (Phase 4)
        Logs/                        <- created at runtime, gitignored
```

- The RPH SDK reference DLL is vendored into `/lib` and referenced via relative
  `<HintPath>`, `Private=False` (RPH's host process supplies the real assembly at
  runtime; the vendored copy is compile-time only).
- RPH loads plugin DLLs from its `Plugins` folder; convention is a same-named
  companion data folder alongside for config. **Resolve that path at runtime from
  the game exe location, not the plugin assembly location** (see status note above).
- Post-build step copies the compiled DLL/PDB into `dist/Plugins/`. Optionally the
  gaming PC's real `Plugins` folder can be a directory junction to `dist\Plugins`
  so config-only tweaks take effect with just a `git pull`.

## 2. Core Systems

### 2.1 Entity spawn registry (kills orphaned-entity gaps)
`EntitySpawnRegistry` is the *only* sanctioned path for tracking spawned objects.
`CalloutBase` exposes protected helpers (`SpawnPed`, `RegisterEntity`,
`RegisterCleanupAction`) that create the entity **and** register it in one call —
concrete callouts never call `new Ped(...)` directly, so there's no ergonomic way
to forget registration.
- Storage: cleanup actions keyed by owner id. `CleanupOwner(id)` wraps each
  deletion in `Safe.Run` so one bad handle can't abort cleanup of the rest;
  idempotent.
- `CleanupAll()` runs at plugin unload as a mod-wide safety net.
- `SpawnPed` also applies `BlockPermanentEvents` + `ClearImmediately` +
  `StandStill` so patient peds behave without needing the Stop The Ped plugin.

### 2.2 Input abstraction (kills keyboard-only/controller-only gaps)
`InputManager` exposes only a logical layer:
```csharp
enum InputAction { AcceptCallout, DeclineCallout, Interact, ConfirmMenuOption }
bool InputManager.IsActionPressed(InputAction action); // edge-triggered, single-tap by construction
```
- Each `InputAction` maps to one key **and** one controller button, via
  `Game.IsKeyDown` / `Game.IsControllerButtonDown` (RPH's edge-triggered checks).
- **Structural rule:** no file outside `Input/` calls raw keyboard/controller
  APIs. All UI and callout logic go through `InputManager.IsActionPressed`. There
  is no "held" primitive, so single-tap is guaranteed, not a rule to remember.
- Bindings are code defaults for now; overridable via config later to dodge
  LSPDFR/Stop The Ped keybind collisions without a recompile.

### 2.3 CalloutBase state machine (kills duplicated-logic and forgotten-edge-case gaps)
```
Dispatched -> AwaitingAcceptDecline -> EnRoute -> OnScene -> Assessment -> Resolution -> CleaningUp -> CleanedUp
                     |-> Declined ----------------------------------------------------^
                     (any state) -> Abandoned -------------------------------------^
```
- `CalloutBase` owns all transition logic; concrete callouts only implement content
  hooks (`DispatchMessage`, `OnAccepted`, `OnSceneArrived`, `IsAssessmentComplete`,
  `OnResolved`) — never cleanup, never the watchdogs.
- `CalloutManager` enforces single-active-callout-at-a-time, ticking exactly one
  instance per frame.
- **Built-in watchdogs, ticked every frame in `CalloutBase.Tick()`:**
  1. *Vehicle-entry watchdog* — `Ped.CurrentVehicle != null` during an on-foot
     interaction routes through the shared Abandoned/cleanup path.
  2. *Walked-away watchdog* — `Vector3.DistanceTo` the scene anchor vs. a config
     threshold (throttled), triggers Abandoned through full cleanup.
  3. *Pause-aware timers* — every timer (incl. the 15s accept/decline auto-cancel)
     advances through one shared `AdvanceTimer` helper that no-ops while paused and
     accumulates via per-frame `Game.FrameTime`, never wall-clock time.

### 2.4 Config loading (kills crash-on-malformed-config gaps)
- `General.xml` (global timers/thresholds) now; one
  `Callouts/<CalloutType_InjuryName>.xml` per callout from Phase 4.
- POCO schema via `System.Xml.Serialization.XmlSerializer`. Two defense layers:
  `Safe.Run` around deserialization (broken XML → defaults), then `ConfigValidator`
  backfills sensible defaults for any missing/out-of-range field.
- `emsmod_reloadconfig` re-reads from disk on demand for tight tuning loops.

### 2.5 Logging / try-catch strategy
- `Log` writes to the RPH console **and** a rolling file under
  `Plugins/EmsMod/Logs/EmsMod.log` (never throws itself).
- `Safe.Run(Action, context)` / `Safe.Run<T>(Func<T>, fallback, context)` is the
  mandatory wrapper for every risky call (spawn, animation, config IO, TTS).
- `EntryPoint.Main`'s tick loop wraps per-tick dispatch in try/catch, with a
  `finally { EntitySpawnRegistry.CleanupAll(); }` so unload/hot-reload never leaves
  entities behind, plus an `AppDomain.UnhandledException` last-resort logger.

### 2.6 Dispatch voice abstraction (kills the Phase-6-rewrite gap)
```csharp
interface IDispatchVoice { void Speak(string line, Action onComplete = null); bool IsSpeaking { get; } void Stop(); void Tick(); }
```
- `DialogueEngine` and callout code depend only on `IDispatchVoice`, selected via a
  factory switch read from `General.xml`.
- `SystemSpeechVoice` drives `System.Speech` via `SpeakAsync` + polling state in
  `Tick()` — **never** the blocking `Speak()`, which would freeze the GameFiber
  and the game. "Poll, never block" applies to every risky API; avoid `async`/
  `await`, which doesn't fit RPH's cooperative single-fiber model.
- `NAudioVoice` (pre-recorded audio) implements the same interface later; swapping
  is a config change plus a new class, zero touches to `DialogueEngine` or callouts.

## 3. Phased Build Order

0. **Scaffolding** — solution, csproj, vendored SDK, minimal `EntryPoint`. ✅ done
1. **Foundation infra** — `Log`/`Safe`, `ConfigLoader`, `EntitySpawnRegistry`,
   `InputManager`, each smoke-tested via console commands. ✅ done
2. **PromptUI + Dialogue/Voice skeleton.** ✅ done
3. **CalloutBase/CalloutManager + trivial TestCallout vertical slice.** ✅ done
4. **First real callout: `CarAccident_Bleeding`** — real config XML, dialogue,
   assessment options, transport-percentage resolution. ← **next**
5. **Second/third callout types** as thin subclasses + config only. Audit point for
   "no duplicated logic."
6. **Polish** — NAudio voice swap, remaining roster, "Call Police" mechanic, tuning
   via config.

**Why this order:** every named gap is cheap to bake into one shared layer once,
but expensive to retrofit across N already-shipped callouts. Hardening infra first
means structural bugs get found and fixed once, before they're copy-pasted into
callout #2 and #3.

## 4. Open Decisions (revisit as we go)
- Popup visual spec (icon vs text, position, whether the 15s countdown shows).
- Default keybindings — only verifiable against LSPDFR/Stop The Ped on the gaming PC.
- "Walked away" threshold + whether it fully cancels or temporarily despawns.
- "Entered vehicle mid-interaction" — full cancel vs. just close any open menu.
- Whether the RPH `Plugins` folder should be a junction to `dist\Plugins`.

## Critical Files
- `src/EmsMod/EntryPoint.cs` — plugin attribute, `Main()` loop, cleanup-on-unload, re-entry guard
- `src/EmsMod/Core/EntitySpawnRegistry.cs` — guarantees zero orphaned entities
- `src/EmsMod/Input/InputManager.cs` — guarantees keyboard+controller parity
- `src/EmsMod/Callouts/CalloutBase.cs` — state machine + watchdogs + pause-safe timers
- `src/EmsMod/Config/ConfigLoader.cs` (+ `ConfigValidator.cs`) — malformed config never crashes
- `src/EmsMod/Utils/ModPaths.cs` — game-exe-based path resolution (not assembly location)
